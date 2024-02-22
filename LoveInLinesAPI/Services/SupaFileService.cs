namespace LoveInLinesAPI.Services
{
    public interface ISupaFileService
    {
        Task<string> UploadFile(IFormFile file);
    }
    public class SupaFileService : ISupaFileService
    {
        private readonly Supabase.Client supabase;

        public SupaFileService(Supabase.Client supabase)
        {
            this.supabase = supabase;
        }

        public async Task<string> UploadFile(IFormFile file)
        {
            Guid guid = Guid.NewGuid();
            string noteID = guid.ToString();
            using var memoryStream = new MemoryStream();

            await file.CopyToAsync(memoryStream);

            var lastIndexOfDot = file.FileName.LastIndexOf('.');

            string extension = file.FileName.Substring(lastIndexOfDot + 1);

            await supabase.Storage.From("notes").Upload(
                memoryStream.ToArray(),
                $"note-{noteID}.{extension}");


            var publicUrl = supabase.Storage.From("notes").GetPublicUrl($"note-{noteID}.{extension}");

            return publicUrl;
        }
    }
}
