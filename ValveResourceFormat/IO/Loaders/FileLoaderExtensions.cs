namespace ValveResourceFormat.IO
{
    /// <summary>
    /// Extension methods for <see cref="IFileLoader"/>.
    /// </summary>
    public static class FileLoaderExtensions
    {
        /// <summary>
        /// Same as <see cref="IFileLoader.LoadFileCompiled"/> but with <see cref="Resource.ReadBlocksOnDemand"/> set,
        /// so only the blocks that are accessed get parsed. The resource is never cached; the caller owns and disposes it,
        /// and must keep it undisposed while accessing its blocks.
        /// </summary>
        /// <param name="fileLoader">Loader that finds the file.</param>
        /// <param name="file">Path to the file to load (without the _c suffix).</param>
        /// <returns>Loaded compiled resource, or <c>null</c> if not found.</returns>
        public static Resource? LoadFileCompiledOnDemand(this IFileLoader fileLoader, string file)
        {
            ArgumentNullException.ThrowIfNull(fileLoader);

            var compiledFile = string.Concat(file, GameFileLoader.CompiledFileSuffix);
            var stream = fileLoader.GetFileStream(compiledFile);

            if (stream == null)
            {
                return null;
            }

            var resource = new Resource
            {
                FileName = compiledFile,
                ReadBlocksOnDemand = true,
            };

            try
            {
                resource.Read(stream);
                return resource;
            }
            catch
            {
                resource.Dispose();
                throw;
            }
        }
    }
}
