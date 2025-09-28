namespace Servy.Core.Helpers
{
    public class DllResource
    {
        public const string Extension = "dll";
        public string FileNameWithoutExtension { get; set; }
        public string Subfolder { get; set; }

        public string ResourceName { get; set; }
        public string TagetFileName { get; set; }
        public string TagetPath { get; set; }
        public bool ShouldCopy { get; set; }
    }
}
