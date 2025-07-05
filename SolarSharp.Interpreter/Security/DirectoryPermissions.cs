namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines the access levels for directories
    /// </summary>
    public enum DirectoryPermissions
    {
        /// <summary>
        /// No access to the directory - cannot list contents or create files
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Can list directory contents but cannot create new files
        /// </summary>
        List = 1,
        
        /// <summary>
        /// Can list directory contents and create new files
        /// </summary>
        ListAndCreateFiles = 2
    }
}