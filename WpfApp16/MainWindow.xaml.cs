using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Timers;
using MailKit.Net.Imap;
using MailKit.Security;
using System.Threading.Tasks;
using MimeKit;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace StudentAttendanceApp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string DataFile = "data.json";
        private AppData _data = new AppData();
        private DateTime _selectedDate = DateTime.Today;
        private Group _selectedStudentGroup;
        private Group _selectedAttendanceGroup;
        private Timer _emailTimer;
        private EmailService _emailService = new EmailService();
        private bool _isEmailChecking;

        public AppData Data => _data;
        public ObservableCollection<Student> FilteredStudents { get; } = new ObservableCollection<Student>();

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; OnPropertyChanged(nameof(SelectedDate)); UpdateAttendanceData(); }
        }

        public Group SelectedStudentGroup
        {
            get => _selectedStudentGroup;
            set { _selectedStudentGroup = value; FilterStudents(); OnPropertyChanged(nameof(SelectedStudentGroup)); }
        }

        public Group SelectedAttendanceGroup
        {
            get => _selectedAttendanceGroup;
            set { _selectedAttendanceGroup = value; FilterAttendanceStudents(); OnPropertyChanged(nameof(SelectedAttendanceGroup)); }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadData();
            SetupEmailTimer();
            SetupBindings();
        }

        private void SetupBindings()
        {
            GroupsList.ItemsSource = Data.Groups;
            StudentsList.ItemsSource = Data.Students;
            LabWorksGrid.ItemsSource = Data.LabWorks;
            EmailMappingsGrid.ItemsSource = Data.EmailMappings;
        }

        private void SetupEmailTimer()
        {
            _emailTimer = new Timer(600000); // 10 минут
            _emailTimer.Elapsed += async (s, e) => await CheckEmailsAsync();
            _emailTimer.Start();
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(DataFile)) return;

                var settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                _data = JsonConvert.DeserializeObject<AppData>(File.ReadAllText(DataFile), settings) ?? new AppData();
                RestoreReferences();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void RestoreReferences()
        {
            foreach (var student in Data.Students)
            {
                student.Group = Data.Groups.FirstOrDefault(g => g.Id == student.GroupId);
            }

            foreach (var mapping in Data.EmailMappings.ToList())
            {
                mapping.Student = Data.Students.FirstOrDefault(s => s.Id == mapping.StudentId);
                if (mapping.Student == null) Data.EmailMappings.Remove(mapping);
            }
        }

        private void SaveData()
        {
            try
            {

                var settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented
                };

                File.WriteAllText(DataFile, JsonConvert.SerializeObject(Data, settings));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }


        }

        private void FilterStudents()
        {
            var view = CollectionViewSource.GetDefaultView(StudentsList.ItemsSource);
            view.Filter = item => item is Student student &&
                (SelectedStudentGroup == null || student.GroupId == SelectedStudentGroup.Id);
        }

        private void FilterAttendanceStudents()
        {
            FilteredStudents.Clear();
            if (SelectedAttendanceGroup == null) return;

            foreach (var student in Data.Students.Where(s => s.GroupId == SelectedAttendanceGroup.Id))
            {
                FilteredStudents.Add(student);
            }
            UpdateAttendanceData();
        }

        private void UpdateAttendanceData()
        {
            if (SelectedAttendanceGroup == null) return;

            foreach (var student in FilteredStudents)
            {
                int pairsCount = SelectedAttendanceGroup.GetPairsForDay(SelectedDate);
                student.UpdateAttendance(SelectedDate, pairsCount);
                // Обновляем текущую запись
                student.CurrentAttendanceRecord = student.AttendanceRecords
                    .FirstOrDefault(r => r.Date.Date == SelectedDate.Date);
            }
            RefreshAttendanceGrid();
        }

        private void RefreshAttendanceGrid()
        {
            AttendanceGrid.Columns.Clear();

            // Добавление базовых колонок
            var checkBoxColumn = new DataGridTemplateColumn
            {
                Header = "Выбор",
                CellTemplate = (DataTemplate)FindResource("CheckBoxTemplate")
            };
            AttendanceGrid.Columns.Add(checkBoxColumn);

            var nameColumn = new DataGridTextColumn
            {
                Header = "Студент",
                Binding = new Binding("Name"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            AttendanceGrid.Columns.Add(nameColumn);

            // Колонки для пар
            int pairsCount = SelectedAttendanceGroup?.GetPairsForDay(SelectedDate) ?? 0;
            for (int i = 0; i < pairsCount; i++)
            {
                var column = new DataGridTemplateColumn
                {
                    Header = $"Пара {i + 1}",
                    CellTemplate = CreateAttendanceTemplate(i)
                };
                AttendanceGrid.Columns.Add(column);
            }
        }

        private DataTemplate CreateAttendanceTemplate(int index)
        {
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            var binding = new Binding($"CurrentAttendanceRecord.AttendanceMarks[{index}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            factory.SetBinding(CheckBox.IsCheckedProperty, binding);
            return new DataTemplate { VisualTree = factory };
        }

        #region Event Handlers
        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(GroupNameBox.Text))
            {
                Data.Groups.Add(new Group { Name = GroupNameBox.Text.Trim() });
                GroupNameBox.Clear();
                SaveData();
            }
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить группу и всех студентов?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (((FrameworkElement)sender).DataContext is Group group)
                {
                    var studentsToRemove = Data.Students.Where(s => s.GroupId == group.Id).ToList();
                    foreach (var student in studentsToRemove)
                    {
                        Data.Students.Remove(student);
                    }
                    Data.Groups.Remove(group);
                    SaveData();
                }
            }
        }

        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStudentGroup != null && !string.IsNullOrWhiteSpace(StudentNameBox.Text))
            {
                var newStudent = new Student
                {
                    Name = StudentNameBox.Text.Trim(),
                    Group = SelectedStudentGroup
                };
                Data.Students.Add(newStudent);
                StudentNameBox.Clear();
                SaveData();
                EmailStudentsCombo.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Выберите группу и введите имя студента!");
            }
        }

        private void DeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить студента?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (((FrameworkElement)sender).DataContext is Student student)
                {
                    Data.Students.Remove(student);
                    SaveData();
                }
            }
        }

        private async void CheckEmailButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckEmailsAsync();
        }

        private async Task CheckEmailsAsync()
        {
            if (_isEmailChecking) return;

            _isEmailChecking = true;
            EmailProgressBar.Visibility = Visibility.Visible;
            EmailStatusText.Text = "Проверка почты...";

            try
            {
                await _emailService.CheckEmailsAsync(Data);
                EmailStatusText.Text = $"Последняя проверка: {DateTime.Now:T}";
                RefreshLabsButton_Click(null, null);
            }
            catch (Exception ex)
            {
                EmailStatusText.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _isEmailChecking = false;
                EmailProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshLabsButton_Click(object sender, RoutedEventArgs e)
        {
            var invalidLabs = Data.LabWorks
                .Where(l => !Data.Students.Any(s => s.Id == l.StudentId))
                .ToList();

            foreach (var lab in invalidLabs)
            {
                Data.LabWorks.Remove(lab);
            }

            LabWorksGrid.Items.Refresh();
            SaveData();
        }

        private void BindEmail_Click(object sender, RoutedEventArgs e)
        {
            if (EmailStudentsCombo.SelectedItem is Student student &&
                !string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                if (Data.EmailMappings.Any(m => m.Email.Equals(EmailTextBox.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Этот email уже привязан!");
                    return;
                }

                Data.EmailMappings.Add(new EmailMapping
                {
                    Email = EmailTextBox.Text.Trim(),
                    Student = student
                });
                SaveData();
                EmailTextBox.Clear();
            }
        }

        private void SaveAttendance_Click(object sender, RoutedEventArgs e)
        {
            SaveData();
            MessageBox.Show("Данные сохранены!");
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveData();
            base.OnClosed(e);
            _emailTimer?.Stop();
            _emailTimer?.Dispose();
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ... остальной код класса MainWindow остается без изменений ...

        #region Missing Event Handlers

        private void ScheduleGroupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WeekTypeCombo_SelectionChanged(null, null);
        }

        private void WeekTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScheduleGroupCombo.SelectedItem is Group selectedGroup &&
                WeekTypeCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                bool isNumerator = selectedItem.Content.ToString() == "Числитель";
                ScheduleGrid.ItemsSource = isNumerator
                    ? selectedGroup.ScheduleNumerator
                    : selectedGroup.ScheduleDenominator;
            }
        }

        private void AddScheduleDay_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleGroupCombo.SelectedItem is Group selectedGroup &&
                WeekTypeCombo.SelectedItem is ComboBoxItem weekTypeItem)
            {
                var newDay = new ScheduleDay();
                bool isNumerator = weekTypeItem.Content.ToString() == "Числитель";

                var targetSchedule = isNumerator
                    ? selectedGroup.ScheduleNumerator
                    : selectedGroup.ScheduleDenominator;

                targetSchedule.Add(newDay);
                SaveData();
            }
        }

        private void DeleteScheduleDay_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ScheduleDay day &&
                ScheduleGroupCombo.SelectedItem is Group selectedGroup)
            {
                selectedGroup.ScheduleNumerator.Remove(day);
                selectedGroup.ScheduleDenominator.Remove(day);
                SaveData();
            }
        }

        private void AttendanceGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Cancel = true; // Отменяем автоматическую генерацию колонок
        }

        private void DownloadLab_Click(object sender, RoutedEventArgs e)
        {
            var filePath = (sender as Button)?.Tag as string;

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{filePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка открытия файла: {ex.Message}");
                }
            }
        }

        #endregion

       
    }
}