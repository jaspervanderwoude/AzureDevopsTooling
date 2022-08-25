namespace AzureDevopsAgentPoolMigrator
{
    public static class ExtensionMethods
    {
        public static string GetRootFolder(this string value)
        {
            // Path always starts with \\
            return value.Split("\\")[1];
        }
    }
}