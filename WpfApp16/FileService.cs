using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MimeKit;


namespace StudentAttendanceApp
{
    public class FileService
    {
        private const string LabPattern = @"Лабораторная работа (\d+)";
        private const string PracticalPattern = @"Практическая работа (\d+)";
        private readonly string _downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "LabWorks");

        public bool ProcessAttachments(MimeMessage message, AppData data, Guid studentId)
        {
            bool hasValidFiles = false;
            Directory.CreateDirectory(_downloadPath);

            foreach (var attachment in message.Attachments)
            {
                if (attachment is not MimePart part) continue;

                var fileName = part.FileName;
                var labMatch = Regex.Match(fileName, LabPattern, RegexOptions.IgnoreCase);
                var practicalMatch = Regex.Match(fileName, PracticalPattern, RegexOptions.IgnoreCase);

                if (!labMatch.Success && !practicalMatch.Success) continue;

                try
                {
                    // Определение типа работы и номера
                    var workType = labMatch.Success ? "Лабораторная работа" : "Практическая работа";
                    var number = labMatch.Success ? labMatch.Groups[1].Value : practicalMatch.Groups[1].Value;

                    // Генерация нового имени файла
                    var newFileName = $"{workType} {number}{Path.GetExtension(fileName)}";
                    var filePath = Path.Combine(_downloadPath, newFileName);

                    // Проверка существования файла
                    if (File.Exists(filePath))
                    {
                        newFileName = $"{workType} {number}_{Guid.NewGuid().ToString().Substring(0, 4)}{Path.GetExtension(fileName)}";
                        filePath = Path.Combine(_downloadPath, newFileName);
                    }

                    // Сохранение файла
                    using (var stream = File.Create(filePath))
                    {
                        part.Content.DecodeTo(stream);
                    }

                    // Проверка на дубликаты перед добавлением
                    if (!data.LabWorks.Any(l => l.FilePath == filePath))
                    {
                        data.LabWorks.Add(new LabWork
                        {
                            StudentId = studentId,
                            Title = newFileName,
                            FilePath = filePath,
                            ReceivedDate = DateTime.Now,
                            MessageId = message.MessageId
                        });
                        hasValidFiles = true;
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Ошибка сохранения файла: {ex.Message}");
                }
            }

            return hasValidFiles;
        }
    }
}