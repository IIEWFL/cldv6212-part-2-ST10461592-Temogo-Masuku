using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace ABCRetailFunctions.Services
{
    public class FileShareStorageService
    {
        //Define the fileshareclient client
        private readonly ShareClient _shareClient;

        //Initialize the constructor
        public FileShareStorageService(string storageConnectionString, string shareName)
        {
            var serviceClient = new ShareServiceClient(storageConnectionString);
            _shareClient = serviceClient.GetShareClient(shareName);
            _shareClient.CreateIfNotExists();
        }

        //upload file to file share                                                                 
        public async Task UploadFileAsync(string fileName, Stream fileStream)
        {
            var directoryClient = _shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient(fileName);

            // Create file with the stream length
            await fileClient.CreateAsync(fileStream.Length);

            // Upload the entire stream at once
            fileStream.Position = 0; // Reset stream position
            await fileClient.UploadRangeAsync(new HttpRange(0, fileStream.Length), fileStream);
        }

        // List all files in the share
        public async Task<List<string>> ListFilesAsync()
        {
            try
            {
                var files = new List<string>();
                var directoryClient = _shareClient.GetRootDirectoryClient();

                await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        files.Add(item.Name);
                    }
                }

                return files;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list files: {ex.Message}", ex);
            }
        }

        // Download a file from the share
        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            try
            {
                var directoryClient = _shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                if (!await fileClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"File '{fileName}' not found.");
                }

                var download = await fileClient.DownloadAsync();
                var memoryStream = new MemoryStream();
                await download.Value.Content.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download file: {ex.Message}", ex);
            }
        }

        // Delete a file from the share
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                var directoryClient = _shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                var response = await fileClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete file: {ex.Message}", ex);
            }
        }

        // Check if a file exists
        public async Task<bool> FileExistsAsync(string fileName)
        {
            try
            {
                var directoryClient = _shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                var response = await fileClient.ExistsAsync();
                return response.Value;
            }
            catch
            {
                return false;
            }
        }
    }
}