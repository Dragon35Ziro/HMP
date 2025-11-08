using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;

namespace StudentAttendanceApp
{
    public class EmailService
    {
        private readonly FileService _fileService = new FileService();

        public async Task CheckEmailsAsync(AppData data)
        {
            using var client = new ImapClient();

            try
            {
                // Подключение к почтовому серверу
                await client.ConnectAsync("imap.yandex.ru", 993, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync("Dragon3576@yandex.ru", "mzwrmzaerkuiwqbj");

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);
                await client.NoOpAsync(); // Синхронизация UID

                var messages = await inbox.FetchAsync(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope);

                foreach (var msg in messages)
                {
                    try
                    {
                        var message = await inbox.GetMessageAsync(msg.UniqueId);
                        var fromEmail = message.From.Mailboxes.FirstOrDefault()?.Address;
                        var mapping = data.EmailMappings.FirstOrDefault(m =>
                            m.Email.Equals(fromEmail, StringComparison.OrdinalIgnoreCase));

                        // Пропускаем письма без привязки к студенту
                        if (mapping == null) continue;

                        // Обработка вложений (возвращает true, если есть валидные файлы)
                        bool hasValidFiles = _fileService.ProcessAttachments(message, data, mapping.StudentId);

                        // Добавляем запись только при наличии нужных файлов
                        if (hasValidFiles && !data.LabWorks.Any(l => l.MessageId == message.MessageId))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.LabWorks.Add(new LabWork
                                {
                                    StudentId = mapping.StudentId,
                                    Title = message.Subject,
                                    ReceivedDate = DateTime.Now,
                                    MessageId = message.MessageId
                                });
                            });
                        }
                    }
                    catch (ImapCommandException ex) when (ex.Message.Contains("uid"))
                    {
                        // Переподключение при недействительных UID
                        await inbox.CloseAsync();
                        await inbox.OpenAsync(FolderAccess.ReadOnly);
                        messages = await inbox.FetchAsync(0, -1, MessageSummaryItems.UniqueId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка обработки письма: {ex.Message}");
                    }
                }
            }
            catch (AuthenticationException)
            {
                MessageBox.Show("Ошибка аутентификации. Проверьте логин/пароль!");
            }
            catch (SocketException)
            {
                MessageBox.Show("Ошибка подключения к серверу!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}");
            }
        }

        private void ProcessMessage(MimeMessage message, AppData data)
        {
            var fromEmail = message.From.Mailboxes.FirstOrDefault()?.Address;
            var mapping = data.EmailMappings.FirstOrDefault(m =>
                m.Email.Equals(fromEmail, StringComparison.OrdinalIgnoreCase));

            if (mapping != null &&
                data.Students.Any(s => s.Id == mapping.StudentId) &&
                !data.LabWorks.Any(l => l.MessageId == message.MessageId))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    data.LabWorks.Add(new LabWork
                    {
                        StudentId = mapping.StudentId,
                        Title = message.Subject,
                        Content = message.TextBody,
                        ReceivedDate = DateTime.Now,
                        MessageId = message.MessageId
                    });
                });
            }
        }
    }
}