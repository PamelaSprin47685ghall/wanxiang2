// 金样生成器：从照抄的旧 Icons.fs 读出「语义名 → Lucide 字形」映射。用法：dotnet fsi tools/gen-golden.fsx
open System.IO
open System.Text.RegularExpressions
let dir = __SOURCE_DIRECTORY__
let src = File.ReadAllText(Path.Combine(dir, "OldIcons.fs.txt"))
let lines =
    [ for m in Regex.Matches(src, @"^    let (\w+) : IBrush -> Control = glyph PackIconLucideKind\.(\w+)", RegexOptions.Multiline) ->
          let n = m.Groups[1].Value
          sprintf "%s%s %s\n" (n.Substring(0, 1).ToUpper()) (n.Substring 1) m.Groups[2].Value ]
File.WriteAllText(Path.Combine(dir, "..", "tests", "golden", "iconset.txt"), String.concat "" lines)
