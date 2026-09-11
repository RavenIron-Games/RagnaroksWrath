// RevProbe — the reverse of apiprobe.
//
// apiprobe checks a hand-written list of surfaces against the game. This checks the OTHER
// direction, exhaustively: open a shipped mod DLL, read its own metadata tables (never execute
// it), enumerate every TypeReference and MemberReference it holds into assembly_valheim,
// assembly_utils or Splatform, and confirm each one still resolves — by full signature, not
// just by name — against the REAL game assemblies loaded through a MetadataLoadContext.
//
// A DLL compiled months ago binds by member SIGNATURE at runtime. Source compiling clean today
// says nothing about a binary already on disk. This is how you check the binary.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

class RevProbe
{
    const BindingFlags AnyFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    // Deliberately excludes Static: a type's implicit type-initializer (.cctor) is ALSO a
    // zero/param ConstructorInfo, and Type.GetConstructor(flags, binder, types, mods) throws
    // AmbiguousMatchException the moment BindingFlags.Static lets both an explicit instance
    // constructor and the auto-generated .cctor satisfy the same param-type match. External IL
    // never calls another type's .cctor directly (the runtime invokes it), so constructor
    // lookups have no reason to search Static in the first place.
    const BindingFlags CtorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly HashSet<string> InScope = new(StringComparer.OrdinalIgnoreCase) { "assembly_valheim", "assembly_utils", "Splatform" };

    static MetadataLoadContext _mlc;
    static readonly Dictionary<string, Assembly> _loadedAsm = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Type> _typeCache = new(StringComparer.Ordinal);

    // ---------------------------------------------------------------------------------------
    // SigType: a signature-decoded type description built from the MOD's own metadata tables.
    // Independent of any live Assembly — resolution against the game happens afterwards.
    // ---------------------------------------------------------------------------------------
    sealed class SigType
    {
        public enum K { Named, Primitive, SZArray, Array, Pointer, ByRef, Pinned, Generic, GenericTypeParam, GenericMethodParam, FunctionPointer }
        public K Kind;
        public string AssemblyHint;   // simple assembly name the type is declared in, or "<self>" for a type the mod itself defines, or null if unknown
        public string FullName;       // Namespace.Outer+Inner ; backtick-arity included for open generics
        public SigType Inner;
        public int ArrayRank;
        public SigType GenericDef;
        public List<SigType> GenericArgs;
        public int ParamIndex;

        public override string ToString()
        {
            switch (Kind)
            {
                case K.SZArray: return Inner + "[]";
                case K.Array: return Inner + "[" + new string(',', Math.Max(0, ArrayRank - 1)) + "]";
                case K.Pointer: return Inner + "*";
                case K.ByRef: return Inner + "&";
                case K.Pinned: return Inner + " pinned";
                case K.Generic: return GenericDef + "<" + string.Join(", ", GenericArgs) + ">";
                case K.GenericTypeParam: return "!" + ParamIndex;
                case K.GenericMethodParam: return "!!" + ParamIndex;
                case K.FunctionPointer: return "<fnptr>";
                default: return FullName ?? "?";
            }
        }
    }

    static bool ContainsGenericParam(SigType s)
    {
        switch (s.Kind)
        {
            case SigType.K.GenericTypeParam:
            case SigType.K.GenericMethodParam:
                return true;
            case SigType.K.SZArray:
            case SigType.K.Array:
            case SigType.K.Pointer:
            case SigType.K.ByRef:
            case SigType.K.Pinned:
                return ContainsGenericParam(s.Inner);
            case SigType.K.Generic:
                return ContainsGenericParam(s.GenericDef) || s.GenericArgs.Any(ContainsGenericParam);
            default:
                return false;
        }
    }

    // Walks a TypeReference's resolution-scope chain (nested types point at their enclosing
    // TypeRef; top-level types point at an AssemblyReference) purely within the MOD's own
    // metadata — no game assembly touched yet.
    static SigType ResolveTypeRefChain(MetadataReader reader, TypeReferenceHandle handle)
    {
        var tr = reader.GetTypeReference(handle);
        string name = reader.GetString(tr.Name);
        var scope = tr.ResolutionScope;
        if (!scope.IsNil && scope.Kind == HandleKind.TypeReference)
        {
            var parent = ResolveTypeRefChain(reader, (TypeReferenceHandle)scope);
            return new SigType { Kind = SigType.K.Named, AssemblyHint = parent.AssemblyHint, FullName = parent.FullName + "+" + name };
        }
        string ns = tr.Namespace.IsNil ? null : reader.GetString(tr.Namespace);
        string full = ns == null ? name : ns + "." + name;
        if (!scope.IsNil && scope.Kind == HandleKind.AssemblyReference)
        {
            var ar = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
            return new SigType { Kind = SigType.K.Named, AssemblyHint = reader.GetString(ar.Name), FullName = full };
        }
        // ModuleReference, Module, or nil scope — not resolvable to an external assembly from here.
        return new SigType { Kind = SigType.K.Named, AssemblyHint = null, FullName = full };
    }

    static SigType ResolveTypeDefChain(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var td = reader.GetTypeDefinition(handle);
        string name = reader.GetString(td.Name);
        var declaring = td.GetDeclaringType();
        if (!declaring.IsNil)
        {
            var parent = ResolveTypeDefChain(reader, declaring);
            return new SigType { Kind = SigType.K.Named, AssemblyHint = "<self>", FullName = parent.FullName + "+" + name };
        }
        string ns = td.Namespace.IsNil ? null : reader.GetString(td.Namespace);
        return new SigType { Kind = SigType.K.Named, AssemblyHint = "<self>", FullName = ns == null ? name : ns + "." + name };
    }

    // Decodes signature blobs (method/field/typespec) found in the MOD's metadata into SigType
    // trees. The MetadataReader passed to each callback is always the MOD's reader — this
    // provider never sees the game's metadata.
    sealed class ModSignatureProvider : ISignatureTypeProvider<SigType, object>
    {
        public SigType GetPrimitiveType(PrimitiveTypeCode code)
        {
            string name = code switch
            {
                PrimitiveTypeCode.Boolean => "System.Boolean",
                PrimitiveTypeCode.Byte => "System.Byte",
                PrimitiveTypeCode.SByte => "System.SByte",
                PrimitiveTypeCode.Char => "System.Char",
                PrimitiveTypeCode.Double => "System.Double",
                PrimitiveTypeCode.Single => "System.Single",
                PrimitiveTypeCode.Int16 => "System.Int16",
                PrimitiveTypeCode.Int32 => "System.Int32",
                PrimitiveTypeCode.Int64 => "System.Int64",
                PrimitiveTypeCode.UInt16 => "System.UInt16",
                PrimitiveTypeCode.UInt32 => "System.UInt32",
                PrimitiveTypeCode.UInt64 => "System.UInt64",
                PrimitiveTypeCode.IntPtr => "System.IntPtr",
                PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                PrimitiveTypeCode.Object => "System.Object",
                PrimitiveTypeCode.String => "System.String",
                PrimitiveTypeCode.TypedReference => "System.TypedReference",
                PrimitiveTypeCode.Void => "System.Void",
                _ => "System.Object",
            };
            return new SigType { Kind = SigType.K.Primitive, AssemblyHint = "mscorlib", FullName = name };
        }

        public SigType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            => ResolveTypeDefChain(reader, handle);

        public SigType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            => ResolveTypeRefChain(reader, handle);

        public SigType GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public SigType GetSZArrayType(SigType elementType) => new SigType { Kind = SigType.K.SZArray, Inner = elementType };
        public SigType GetArrayType(SigType elementType, ArrayShape shape) => new SigType { Kind = SigType.K.Array, Inner = elementType, ArrayRank = shape.Rank };
        public SigType GetPointerType(SigType elementType) => new SigType { Kind = SigType.K.Pointer, Inner = elementType };
        public SigType GetByReferenceType(SigType elementType) => new SigType { Kind = SigType.K.ByRef, Inner = elementType };
        public SigType GetPinnedType(SigType elementType) => new SigType { Kind = SigType.K.Pinned, Inner = elementType };
        public SigType GetGenericInstantiation(SigType genericType, ImmutableArray<SigType> typeArguments)
            => new SigType { Kind = SigType.K.Generic, GenericDef = genericType, GenericArgs = typeArguments.ToList() };
        public SigType GetGenericMethodParameter(object genericContext, int index) => new SigType { Kind = SigType.K.GenericMethodParam, ParamIndex = index };
        public SigType GetGenericTypeParameter(object genericContext, int index) => new SigType { Kind = SigType.K.GenericTypeParam, ParamIndex = index };
        public SigType GetModifiedType(SigType modifier, SigType unmodifiedType, bool isRequired) => unmodifiedType;
        public SigType GetFunctionPointerType(MethodSignature<SigType> signature) => new SigType { Kind = SigType.K.FunctionPointer };
    }

    static readonly ModSignatureProvider Provider = new ModSignatureProvider();

    // ---------------------------------------------------------------------------------------
    // Resolution against the REAL game assemblies (1.0.12), via MetadataLoadContext.
    // ---------------------------------------------------------------------------------------

    static Assembly LoadByName(string simpleName)
    {
        if (simpleName == null || simpleName == "<self>") return null;
        if (_loadedAsm.TryGetValue(simpleName, out var cached)) return cached;
        Assembly a;
        try { a = _mlc.LoadFromAssemblyName(simpleName); }
        catch { a = null; }
        _loadedAsm[simpleName] = a;
        return a;
    }

    static Type FindNamedType(string assemblyHint, string fullName)
    {
        string key = (assemblyHint ?? "?") + "|" + fullName;
        if (_typeCache.TryGetValue(key, out var cached)) return cached;

        Type t = null;
        if (assemblyHint != null && assemblyHint != "<self>")
        {
            var asm = LoadByName(assemblyHint);
            if (asm != null) t = asm.GetType(fullName, false);
        }
        if (t == null)
        {
            // Fall back across every assembly already resolved into the context — covers
            // type-forwarding and cases where the hinted assembly name didn't pan out.
            foreach (var asm in _mlc.GetAssemblies())
            {
                t = asm.GetType(fullName, false);
                if (t != null) break;
            }
        }
        _typeCache[key] = t;
        return t;
    }

    static Type Resolve(SigType s)
    {
        switch (s.Kind)
        {
            case SigType.K.Named:
            case SigType.K.Primitive:
                return FindNamedType(s.AssemblyHint, s.FullName);
            case SigType.K.SZArray:
                { var e = Resolve(s.Inner); return e?.MakeArrayType(); }
            case SigType.K.Array:
                { var e = Resolve(s.Inner); return e?.MakeArrayType(Math.Max(1, s.ArrayRank)); }
            case SigType.K.Pointer:
                { var e = Resolve(s.Inner); return e?.MakePointerType(); }
            case SigType.K.ByRef:
                { var e = Resolve(s.Inner); return e?.MakeByRefType(); }
            case SigType.K.Pinned:
                return Resolve(s.Inner);
            case SigType.K.Generic:
                {
                    var def = Resolve(s.GenericDef);
                    if (def == null) return null;
                    var args = new Type[s.GenericArgs.Count];
                    for (int i = 0; i < args.Length; i++)
                    {
                        args[i] = Resolve(s.GenericArgs[i]);
                        if (args[i] == null) return null;
                    }
                    try { return def.MakeGenericType(args); } catch { return null; }
                }
            default:
                return null; // generic params, function pointers: not resolvable standalone
        }
    }

    // ---------------------------------------------------------------------------------------
    // Per-DLL walk
    // ---------------------------------------------------------------------------------------

    class DllReport
    {
        public string Path, ModName, ModVersion;
        public int TypeRefsChecked, MemberRefsChecked;
        public List<string> Misses = new();
        public List<string> Skips = new();
        public string Verdict;
    }

    static DllReport ProcessDll(string path)
    {
        var report = new DllReport { Path = path };
        byte[] bytes = File.ReadAllBytes(path);
        using var ms = new MemoryStream(bytes);
        using var peReader = new PEReader(ms);
        if (!peReader.HasMetadata)
        {
            report.Verdict = "INCONCLUSIVE — not a managed assembly (no CLI metadata)";
            return report;
        }
        var reader = peReader.GetMetadataReader();
        var asmDef = reader.GetAssemblyDefinition();
        report.ModName = reader.GetString(asmDef.Name);
        report.ModVersion = asmDef.Version.ToString();

        // --- TypeReference table: every named type the mod holds a token for ---
        foreach (var h in reader.TypeReferences)
        {
            var sig = ResolveTypeRefChain(reader, h);
            if (sig.AssemblyHint == null || !InScope.Contains(sig.AssemblyHint)) continue;
            report.TypeRefsChecked++;
            var t = FindNamedType(sig.AssemblyHint, sig.FullName);
            if (t == null)
                report.Misses.Add($"MISSING TYPE  {sig.AssemblyHint}!{sig.FullName}   <-- TypeLoadException at runtime");
        }

        // --- MemberReference table: every field/method the mod holds a token for ---
        foreach (var h in reader.MemberReferences)
        {
            var mr = reader.GetMemberReference(h);
            string memberName = reader.GetString(mr.Name);
            var parentHandle = mr.Parent;

            SigType declaringSig;
            if (parentHandle.Kind == HandleKind.TypeReference)
                declaringSig = ResolveTypeRefChain(reader, (TypeReferenceHandle)parentHandle);
            else if (parentHandle.Kind == HandleKind.TypeSpecification)
            {
                try { declaringSig = reader.GetTypeSpecification((TypeSpecificationHandle)parentHandle).DecodeSignature(Provider, null); }
                catch { continue; }
            }
            else
                continue; // MethodDefinition parent (vararg call site) — not a cross-assembly ref

            if (declaringSig.AssemblyHint == null || !InScope.Contains(declaringSig.AssemblyHint)) continue;

            var kind = mr.GetKind();
            Type declaringType = Resolve(declaringSig);

            if (kind == MemberReferenceKind.Field)
            {
                report.MemberRefsChecked++;
                SigType fieldSig;
                try { fieldSig = mr.DecodeFieldSignature(Provider, null); }
                catch (Exception ex) { report.Skips.Add($"{declaringSig}.{memberName}  (field signature decode failed: {ex.Message})"); continue; }

                string label = $"{declaringSig}.{memberName} : {fieldSig}";
                if (declaringType == null)
                {
                    report.Misses.Add($"MISSING TYPE  {declaringSig}   (declaring type of field {memberName})");
                    continue;
                }
                var fi = declaringType.GetField(memberName, AnyFlags);
                if (fi == null)
                {
                    report.Misses.Add($"MISSING FIELD  {label}   <-- MissingFieldException at runtime");
                    continue;
                }
                var fieldType = Resolve(fieldSig);
                if (fieldType != null && !fi.FieldType.Equals(fieldType))
                    report.Misses.Add($"FIELD RETYPED  {label}   <-- now {fi.FieldType.FullName}; MissingFieldException at runtime");
            }
            else
            {
                report.MemberRefsChecked++;
                MethodSignature<SigType> methodSig;
                try { methodSig = mr.DecodeMethodSignature(Provider, null); }
                catch (Exception ex) { report.Skips.Add($"{declaringSig}.{memberName}(...)  (method signature decode failed: {ex.Message})"); continue; }

                var paramSigs = methodSig.ParameterTypes;
                string paramList = string.Join(", ", paramSigs.Select(p => p.ToString()));
                string label = $"{declaringSig}.{memberName}({paramList})";

                if (declaringType == null)
                {
                    report.Misses.Add($"MISSING TYPE  {declaringSig}   (declaring type of {memberName})");
                    continue;
                }

                bool hasOpenParam = paramSigs.Any(ContainsGenericParam);
                var paramTypes = new Type[paramSigs.Length];
                bool allResolved = true;
                for (int i = 0; i < paramSigs.Length; i++)
                {
                    paramTypes[i] = hasOpenParam ? null : Resolve(paramSigs[i]);
                    if (!hasOpenParam && paramTypes[i] == null) allResolved = false;
                }

                if (!hasOpenParam && !allResolved)
                {
                    report.Skips.Add($"{label}  (a parameter type could not be resolved in the game assemblies — not counted as a miss)");
                    continue;
                }

                if (hasOpenParam)
                {
                    // Reflection's GetMethod(name, Type[]) cannot overload-match against an
                    // open generic method parameter. Fall back to name + arity, best-effort.
                    var candidates = declaringType.GetMember(memberName, MemberTypes.Method | MemberTypes.Constructor, AnyFlags)
                        .OfType<MethodBase>()
                        .Where(m => m.GetParameters().Length == paramSigs.Length)
                        .ToList();
                    if (candidates.Count == 0)
                        report.Misses.Add($"MISSING METHOD (generic — matched by name+arity only)  {label}");
                    else
                        report.Skips.Add($"{label}  (generic method; matched {candidates.Count} candidate(s) by name+arity only — not a full signature check)");
                    continue;
                }

                MethodBase found = memberName == ".ctor" || memberName == ".cctor"
                    ? (MethodBase)declaringType.GetConstructor(CtorFlags, null, paramTypes, null)
                    : declaringType.GetMethod(memberName, AnyFlags, null, paramTypes, null);

                if (found == null)
                {
                    report.Misses.Add($"MISSING METHOD  {label}   <-- MissingMethodException at runtime");
                    var existing = declaringType.GetMember(memberName, AnyFlags).OfType<MethodBase>()
                        .Select(m => $"{memberName}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                    foreach (var e in existing.Take(8))
                        report.Misses.Add("      exists instead: " + e);
                }
                else if (found is MethodInfo mi && !ContainsGenericParam(methodSig.ReturnType))
                {
                    var retType = Resolve(methodSig.ReturnType);
                    if (retType != null && !mi.ReturnType.Equals(retType))
                        report.Misses.Add($"RETURN TYPE CHANGED  {label} : {methodSig.ReturnType}   <-- now returns {mi.ReturnType.FullName}; MissingMethodException at runtime");
                }
            }
        }

        int hardMisses = report.Misses.Count(l => !l.StartsWith("      exists instead:"));
        if (hardMisses > 0)
            report.Verdict = $"BROKEN ON 1.0.12 — {hardMisses} unresolved reference(s)";
        else if (report.TypeRefsChecked == 0 && report.MemberRefsChecked == 0)
            report.Verdict = "INCONCLUSIVE — no references into assembly_valheim / assembly_utils / Splatform found in this DLL";
        else
            report.Verdict = "BINDS CLEAN";

        return report;
    }

    static void PrintReport(DllReport r)
    {
        Console.WriteLine("=== " + r.Path + " ===");
        if (r.ModName != null)
            Console.WriteLine($"  assembly: {r.ModName} {r.ModVersion}");
        Console.WriteLine($"  type refs checked (assembly_valheim/assembly_utils/Splatform): {r.TypeRefsChecked}");
        Console.WriteLine($"  member refs checked (same scope): {r.MemberRefsChecked}");
        if (r.Skips.Count > 0)
        {
            Console.WriteLine($"  {r.Skips.Count} skipped (tool could not fully verify — not counted as misses):");
            foreach (var s in r.Skips) Console.WriteLine("    - " + s);
        }
        if (r.Misses.Count > 0)
        {
            Console.WriteLine($"  {r.Misses.Count(l => !l.StartsWith("      exists instead:"))} MISS(ES):");
            foreach (var m in r.Misses) Console.WriteLine("    " + m);
        }
        Console.WriteLine("  VERDICT: " + r.Verdict);
        Console.WriteLine();
    }

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: RevProbe.exe <gameManagedDir> <dllOrDir> [<dllOrDir> ...]");
            return 2;
        }
        string managed = args[0];
        if (!Directory.Exists(managed)) { Console.WriteLine("No Managed dir: " + managed); return 2; }

        // Same dedupe apiprobe uses: the game ships its own Mono mscorlib; feed the host
        // runtime's copy in too only as a fallback for names the game folder doesn't have, and
        // let the game's own assemblies win on a name collision.
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.GetFiles(managed, "*.dll"))
            byName[Path.GetFileNameWithoutExtension(f)] = f;
        foreach (var f in Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"))
            if (!byName.ContainsKey(Path.GetFileNameWithoutExtension(f)))
                byName[Path.GetFileNameWithoutExtension(f)] = f;

        _mlc = new MetadataLoadContext(new PathAssemblyResolver(byName.Values.ToList()), "mscorlib");

        var targets = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if (Directory.Exists(a))
                targets.AddRange(Directory.GetFiles(a, "*.dll", SearchOption.AllDirectories));
            else if (File.Exists(a))
                targets.Add(a);
            else
                Console.WriteLine("SKIP (not found): " + a);
        }

        Console.WriteLine("Game assemblies: " + managed);
        Console.WriteLine($"Checking {targets.Count} DLL(s) for references into: " + string.Join(", ", InScope));
        Console.WriteLine();

        int anyBroken = 0;
        foreach (var dll in targets)
        {
            DllReport r;
            try { r = ProcessDll(dll); }
            catch (Exception ex)
            {
                r = new DllReport { Path = dll, Verdict = "INCONCLUSIVE — tool threw: " + ex.GetType().Name + ": " + ex.Message };
            }
            PrintReport(r);
            if (r.Verdict != null && r.Verdict.StartsWith("BROKEN")) anyBroken = 1;
        }

        return anyBroken;
    }
}
