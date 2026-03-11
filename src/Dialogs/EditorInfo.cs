namespace WorkspaceFiles
{
    /// <summary>
    /// Represents a Visual Studio editor registered in the Config hive.
    /// </summary>
    internal sealed class EditorInfo
    {
        public EditorInfo(Guid editorGuid, string displayName)
        {
            EditorGuid = editorGuid;
            DisplayName = displayName;
        }

        public Guid EditorGuid { get; }
        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
