namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Base class for execution-related errors
    /// </summary>
    public abstract class ExecutionError
    {
        public abstract string Message { get; }

        public override string ToString() => Message;
    }

    /// <summary>
    /// Security-related execution errors
    /// </summary>
    public abstract class SecurityError : ExecutionError { }

    /// <summary>
    /// Manifest-related errors
    /// </summary>
    public abstract class ManifestError : ExecutionError { }

    /// <summary>
    /// No manifest found for script
    /// </summary>
    public sealed class ManifestNotFound : ManifestError
    {
        public string Directory { get; }

        public ManifestNotFound(string directory)
        {
            Directory = directory;
        }

        public override string Message
        {
            get { return $"No manifest.json found in directory: {Directory}"; }
        }
    }

    /// <summary>
    /// Manifest signature verification failed
    /// </summary>
    public sealed class ManifestVerificationFailed : ManifestError
    {
        public string Details { get; }

        public ManifestVerificationFailed(string details)
        {
            Details = details;
        }

        public override string Message
        {
            get { return $"Manifest verification failed: {Details}"; }
        }
    }

    /// <summary>
    /// Invalid manifest format
    /// </summary>
    public sealed class InvalidManifest : ManifestError
    {
        public string Details { get; }

        public InvalidManifest(string details)
        {
            Details = details;
        }

        public override string Message
        {
            get { return $"Invalid manifest: {Details}"; }
        }
    }

    /// <summary>
    /// No execution context available
    /// </summary>
    public sealed class NoExecutionContext : SecurityError
    {
        public override string Message
        {
            get { return "No execution context available"; }
        }
    }

    /// <summary>
    /// Certificate-related error
    /// </summary>
    public sealed class CertificateError : SecurityError
    {
        public string Details { get; }

        public CertificateError(string details)
        {
            Details = details;
        }

        public override string Message
        {
            get { return $"Certificate error: {Details}"; }
        }
    }

    /// <summary>
    /// Script loading error
    /// </summary>
    public sealed class ScriptLoadError : ExecutionError
    {
        public string FileName { get; }
        public string Details { get; }

        public ScriptLoadError(string fileName, string details)
        {
            FileName = fileName;
            Details = details;
        }

        public override string Message
        {
            get { return $"Failed to load script '{FileName}': {Details}"; }
        }
    }
}
