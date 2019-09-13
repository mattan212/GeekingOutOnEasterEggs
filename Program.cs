using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace HiddenBinaryMessageDecryptor
{
    class Program
    {
        private const string COGNITIVE_API_ENDPOINT = "https://westcentralus.api.cognitive.microsoft.com";

        private const string APPLE_EASTER_EGG = "https://files.geektime.co.il/wp-content/uploads/2019/09/BSOD-1568260548.jpg";

        private const string SILICON_VALLEY_EASTER_EGG = "https://i.kinja-img.com/gawker-media/image/upload/m5zkaqspub0cgocxjhxt.png";

        static void Main(string[] args)
        {
            //var apiKey = ConfigurationSettings.AppSettings["apiKey"];
            var apiKey = "INSERT_YOUR_COMPUTER_VISION_API_KEY_HERE";

            var computerVision = new ComputerVisionClient(new ApiKeyServiceClientCredentials(apiKey))
            {
                Endpoint = COGNITIVE_API_ENDPOINT
            };

            var extractedText = ExtractRemoteTextAsync(computerVision, APPLE_EASTER_EGG)
                .GetAwaiter().GetResult().Replace('\n', ' ');

            var binaryWords = extractedText.Split(' ').Where(x => x.All(y => y == '0' || y == '1' || y == ' '))
                    .Select(x => x.Trim()).ToArray();

            var deciphered = Decipher(AggregateBinaryWords(binaryWords));

            Console.WriteLine(deciphered);

            Console.ReadKey();
        }

        private static string Decipher(string binaryCipher)
        {
            var words = binaryCipher.Trim().Replace('\r', ' ')
                .Replace('\n', ' ').Split(' ');

            var res = "";
            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    res += " ";
                }
                else
                {
                    try
                    {
                        var c = (char)Convert.ToInt32(word, 2);
                        res += c;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Invalid ASCII of binary word: {0}", word);
                    }
                }
            }

            return res;
        }

        private static string AggregateBinaryWords(string[] binaryWords)
        {
            var res = "";
            for (var i = 0; i < binaryWords.Length - 1; i++)
            {
                if (binaryWords[i].Length == 8)
                {
                    res += binaryWords[i] + " ";
                }
                else if (binaryWords[i].Length + binaryWords[i + 1].Length == 8)
                {
                    res += binaryWords[i] + binaryWords[i + 1] + " ";
                }
            }
            return res;
        }
        private static async Task<string> ExtractRemoteTextAsync(ComputerVisionClient computerVision, string imageUrl)
        {
            if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                throw new Exception($"Invalid remoteImageUrl: {imageUrl}");
            }

            var textHeaders = await computerVision.BatchReadFileAsync(imageUrl);

            return await GetTextAsync(computerVision, textHeaders.OperationLocation);
        }

        // Retrieve the recognized text
        private static async Task<string> GetTextAsync(ComputerVisionClient computerVision, string operationLocation)
        {
            var numberOfCharsInOperationId = 36;

            var operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

            var result = await computerVision.GetReadOperationResultAsync(operationId);

            var maxRetries = 10;

            for (var i = 0; i < maxRetries; i++)
            {
                if (result.Status == TextOperationStatusCodes.Running || result.Status == TextOperationStatusCodes.NotStarted)
                {
                    await Task.Delay(1000);
                }
                else
                {
                    break;
                }
                result = await computerVision.GetReadOperationResultAsync(operationId);
            }

            return result.RecognitionResults.SelectMany(recResult => recResult.Lines)
                .Aggregate("", (current, line) => current + $"{line.Text}\n");
        }
    }
}
