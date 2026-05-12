using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ElevateED.Services
{
    public class PdfExtractionService
    {
        /// <summary>
        /// Extracts text from a PDF file. Falls back to basic text if no PDF library available.
        /// Install-Package UglyToad.PdfPig for full PDF support.
        /// </summary>
        public async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLower();

            try
            {
                if (extension == ".pdf")
                {
                    return await ExtractPdfTextAsync(fileStream);
                }
                else if (extension == ".txt")
                {
                    return await ExtractTextFileAsync(fileStream);
                }
                else if (extension == ".docx")
                {
                    return "Word document upload not yet supported. Please convert to PDF or TXT.";
                }
                else
                {
                    return "Unsupported file format. Please upload PDF or TXT files.";
                }
            }
            catch (Exception ex)
            {
                return "Error extracting text: " + ex.Message;
            }
        }

        private async Task<string> ExtractPdfTextAsync(Stream fileStream)
        {
            try
            {
                // Try using PdfPig if available
                using (var document = UglyToad.PdfPig.PdfDocument.Open(fileStream))
                {
                    var sb = new StringBuilder();
                    foreach (var page in document.GetPages())
                    {
                        var pageText = page.Text;
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            sb.AppendLine(pageText);
                        }
                    }
                    var text = sb.ToString().Trim();
                    return !string.IsNullOrEmpty(text) ? text : "No text could be extracted from this PDF. The file may be scanned or image-based.";
                }
            }
            catch
            {
                // Fallback: return message about PdfPig
                return "PDF extraction requires the PdfPig library. Please install: Install-Package UglyToad.PdfPig";
            }
        }

        private async Task<string> ExtractTextFileAsync(Stream fileStream)
        {
            using (var reader = new StreamReader(fileStream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}