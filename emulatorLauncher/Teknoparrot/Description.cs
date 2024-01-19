namespace TeknoParrotUi.Common
{
    /*
    public enum GPUSTATUS
    {
        NO_INFO,
        // no support at all
        NO,
        // runs fine
        OK,
        // requires fix from Discord
        WITH_FIX,
        // runs but with issues
        HAS_ISSUES
    }
    */
    public class Description
    {
        public string platform;
        public string release_year;
        public string nvidia;
        public string nvidia_issues;
        public string amd;
        public string amd_issues;
        public string intel;
        public string intel_issues;
        public string general_issues;
    }
}
