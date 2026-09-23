using System;
using System.Collections.Generic;

namespace BaiguVN
{
    public sealed class ResourceName
    {
        public readonly string Id;
        public readonly string Scope;
        public readonly string Kind;
        public readonly string Subject;
        public readonly string Variant;

        internal ResourceName(string id, string scope, string kind, string subject, string variant)
        {
            Id = id;
            Scope = scope;
            Kind = kind;
            Subject = subject;
            Variant = variant;
        }
    }

    /// <summary>String-only naming rules; no asset loading or editor dependency.</summary>
    public static class ResourceNaming
    {
        public const string RootPath = "Assets/_Game/ResourceLibrary";

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "background", "portrait", "cg", "prop", "effect", "bgm", "se", "ambience"
        };

        /// <summary>
        /// Includes malformed paths beneath the managed root so the importer can
        /// report their errors. This method does not certify an asset name.
        /// </summary>
        public static bool IsManagedPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string normalized = assetPath.Replace('\\', '/');
            return normalized == RootPath || normalized.StartsWith(RootPath + "/", StringComparison.Ordinal);
        }

        public static bool TryParseAssetPath(string assetPath, out ResourceName resource, out string error)
        {
            resource = null;
            error = null;
            if (string.IsNullOrWhiteSpace(assetPath))
                return Fail("资源路径不能为空。", out error);
            if (assetPath != assetPath.Trim())
                return Fail("资源路径首尾不能有空白。", out error);
            if (!IsManagedPath(assetPath))
                return Fail("路径不属于自动资源库；请使用以 " + RootPath + "/ 开头的项目相对路径。", out error);

            string normalized = assetPath.Replace('\\', '/');
            string relative = normalized.Length > RootPath.Length
                ? normalized.Substring(RootPath.Length + 1) : "";
            string[] segments = relative.Split(new[] { '/' }, StringSplitOptions.None);
            if (segments.Length != 3 || string.IsNullOrEmpty(segments[0]) ||
                string.IsNullOrEmpty(segments[1]) || string.IsNullOrEmpty(segments[2]))
                return Fail("资源库内必须严格使用 <scope>/<kind>/<文件名> 三层路径，不能增加子目录或省略层级。", out error);

            string folderScope = segments[0];
            string folderKind = segments[1];
            string fileName = segments[2];
            if (!IsScope(folderScope))
                return Fail("scope 目录名无效：须以小写英文字母开头，只含小写字母、数字和单个下划线或连字符。", out error);
            if (!Kinds.Contains(folderKind))
                return Fail("kind 目录名无效：只允许 background、portrait、cg、prop、effect、bgm、se、ambience。", out error);

            int dot = fileName.LastIndexOf('.');
            if (dot <= 0 || dot == fileName.Length - 1)
                return Fail("资源文件必须有受支持的扩展名。", out error);
            string id = fileName.Substring(0, dot);
            string extension = fileName.Substring(dot + 1);
            if (!TryParseId(id, out ResourceName parsed, out error)) return false;
            if (parsed.Scope != folderScope)
                return Fail("文件名 scope 与目录不一致：文件名是 " + parsed.Scope + "，目录是 " + folderScope + "。", out error);
            if (parsed.Kind != folderKind)
                return Fail("文件名 kind 与目录不一致：文件名是 " + parsed.Kind + "，目录是 " + folderKind + "。", out error);
            if (!IsExtensionAllowed(parsed.Kind, extension, out error)) return false;

            resource = parsed;
            return true;
        }

        public static bool TryParseId(string id, out ResourceName resource, out string error)
        {
            resource = null;
            error = null;
            if (string.IsNullOrWhiteSpace(id))
                return Fail("资源 ID 不能为空。", out error);
            if (id.IndexOf('/') >= 0 || id.IndexOf('\\') >= 0 || id.IndexOf('.') >= 0)
                return Fail("资源 ID 必须是完整文件名去掉扩展名，不能包含目录、斜杠或句点。", out error);

            string[] parts = id.Split(new[] { "__" }, StringSplitOptions.None);
            if (parts.Length != 4)
                return Fail("资源 ID 必须有且只有四段：<scope>__<kind>__<subject>__<variant>。", out error);
            if (!IsScope(parts[0]))
                return Fail("资源 ID 的 scope 无效：使用 shared 或小写路线 ID；须以字母开头，不允许连续分隔符。", out error);
            if (!Kinds.Contains(parts[1]))
                return Fail("资源 ID 的 kind 无效：只允许 background、portrait、cg、prop、effect、bgm、se、ambience。", out error);
            if (!IsWordToken(parts[2]))
                return Fail("资源 ID 的 subject 无效：只允许小写英文字母、数字和词间单下划线；不允许空格、中文、连字符或空值。", out error);
            if (!IsWordToken(parts[3]))
                return Fail("资源 ID 的 variant 无效：只允许小写英文字母、数字和词间单下划线；不允许空格、中文、连字符或空值。", out error);

            resource = new ResourceName(id, parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        private static bool IsExtensionAllowed(string kind, string extension, out string error)
        {
            error = null;
            if (kind == "bgm" || kind == "se" || kind == "ambience")
            {
                if (EqualsExtension(extension, "wav") || EqualsExtension(extension, "ogg")) return true;
                return Fail(kind + " 资源只支持 WAV 或 OGG，当前扩展名为 ." + extension + "。", out error);
            }
            if (kind == "portrait" || kind == "prop" || kind == "effect")
            {
                if (EqualsExtension(extension, "png")) return true;
                return Fail(kind + " 资源必须使用 PNG，以支持透明画面，当前扩展名为 ." + extension + "。", out error);
            }
            if (EqualsExtension(extension, "png") || EqualsExtension(extension, "jpg") || EqualsExtension(extension, "jpeg"))
                return true;
            return Fail(kind + " 资源只支持 PNG、JPG 或 JPEG，当前扩展名为 ." + extension + "。", out error);
        }

        private static bool EqualsExtension(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static bool IsWordToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            bool separator = true;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (IsLowerLetter(character) || IsDigit(character)) separator = false;
                else if (character == '_' && !separator && i < value.Length - 1) separator = true;
                else return false;
            }
            return !separator;
        }

        private static bool IsScope(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsLowerLetter(value[0])) return false;
            bool separator = false;
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                if (IsLowerLetter(character) || IsDigit(character)) separator = false;
                else if ((character == '_' || character == '-') && !separator && i < value.Length - 1) separator = true;
                else return false;
            }
            return !separator;
        }

        private static bool IsLowerLetter(char value) => value >= 'a' && value <= 'z';
        private static bool IsDigit(char value) => value >= '0' && value <= '9';
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
