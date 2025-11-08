using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;


namespace StudentAttendanceApp
{
    public class AppData
    {
        [JsonProperty("EmailMappings")]
        public ObservableCollection<EmailMapping> EmailMappings { get; set; } = new ObservableCollection<EmailMapping>();

        [JsonProperty("Students")]
        public ObservableCollection<Student> Students { get; set; } = new ObservableCollection<Student>();

        public ObservableCollection<Group> Groups { get; set; } = new ObservableCollection<Group>();
        public ObservableCollection<LabWork> LabWorks { get; set; } = new ObservableCollection<LabWork>();
        //public ObservableCollection<AttendanceRecord> AttendanceRecords { get; set; } = new ObservableCollection<AttendanceRecord>();

        [JsonProperty("AttendanceRecords")]
        public ObservableCollection<AttendanceRecord> AttendanceRecords { get; set; }
        = new ObservableCollection<AttendanceRecord>();
    }




    public class ScheduleDay : INotifyPropertyChanged
    {
        private DayOfWeek _day;
        private int _pairsCount;

        public DayOfWeek Day
        {
            get => _day;
            set { _day = value; OnPropertyChanged(nameof(Day)); }
        }

        public int PairsCount
        {
            get => _pairsCount;
            set { _pairsCount = value; OnPropertyChanged(nameof(PairsCount)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Group : INotifyPropertyChanged
    {
        private Guid _id;
        private string _name;
        private ObservableCollection<ScheduleDay> _scheduleNumerator = new ObservableCollection<ScheduleDay>();
        private ObservableCollection<ScheduleDay> _scheduleDenominator = new ObservableCollection<ScheduleDay>();
        private ObservableCollection<Student> _students = new ObservableCollection<Student>();

        [JsonProperty]
        public Guid Id
        {
            get => _id;
            private set
            {
                if (_id == value) return;
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [JsonProperty]
        public ObservableCollection<ScheduleDay> ScheduleNumerator
        {
            get => _scheduleNumerator;
            set
            {
                _scheduleNumerator = value;
                OnPropertyChanged(nameof(ScheduleNumerator));
            }
        }

        [JsonProperty]
        public ObservableCollection<ScheduleDay> ScheduleDenominator
        {
            get => _scheduleDenominator;
            set
            {
                _scheduleDenominator = value;
                OnPropertyChanged(nameof(ScheduleDenominator));
            }
        }

        [JsonIgnore]
        public ObservableCollection<Student> Students
        {
            get => _students;
            set
            {
                _students = value;
                OnPropertyChanged(nameof(Students));
            }
        }

        public Group()
        {
            // Генерируем ID только для новых групп
            if (_id == Guid.Empty)
                _id = Guid.NewGuid();
        }

        public int GetPairsForDay(DateTime date)
        {
            bool isNumerator = IsNumeratorWeek(date);
            var schedule = isNumerator ? ScheduleNumerator : ScheduleDenominator;
            return schedule.FirstOrDefault(s => s.Day == date.DayOfWeek)?.PairsCount ?? 0;
        }

        public static bool IsNumeratorWeek(DateTime date)
        {
            var startDate = new DateTime(2023, 9, 1);
            int totalWeeks = (int)((date - startDate).TotalDays / 7);
            return totalWeeks % 2 == 0;
        }

        public void RefreshStudents()
        {
            OnPropertyChanged(nameof(Students));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Student : INotifyPropertyChanged
    {
        private Guid _id;
        private string _name;
        private Group _group;
        private bool _isSelected;
        private ObservableCollection<AttendanceRecord> _attendanceRecords = new ObservableCollection<AttendanceRecord>();

        [JsonProperty]
        public Guid Id
        {
            get => _id;
            private set
            {
                if (_id == value) return;
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public Guid GroupId { get; set; }

        [JsonIgnore]
        public Group Group
        {
            get => _group;
            set
            {
                if (_group == value) return;
                _group = value;
                GroupId = value?.Id ?? Guid.Empty;
                OnPropertyChanged(nameof(Group));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        [JsonProperty]
        public ObservableCollection<AttendanceRecord> AttendanceRecords
        {
            get => _attendanceRecords;
            set
            {
                if (_attendanceRecords == value) return;
                _attendanceRecords = value;
                OnPropertyChanged(nameof(AttendanceRecords));
            }
        }

        [JsonIgnore]
        public int TotalClassesAttended =>
            AttendanceRecords.Sum(r => r.AttendanceMarks?.Count(m => m) ?? 0);

        [JsonIgnore]
        public int TotalLabsSubmitted { get; set; }


        public Student()
        {
            // Генерируем ID только для новых студентов
            if (_id == Guid.Empty)
                _id = Guid.NewGuid();
        }

        public void UpdateAttendance(DateTime date, int pairsCount)
        {
            var record = AttendanceRecords.FirstOrDefault(r => r.Date.Date == date.Date);
            if (record == null)
            {
                record = new AttendanceRecord
                {
                    Date = date,
                    StudentId = this.Id // Добавьте привязку к студенту
                };
                AttendanceRecords.Add(record);
            }

            // Синхронизация количества пар
            while (record.AttendanceMarks.Count < pairsCount)
                record.AttendanceMarks.Add(false);

            while (record.AttendanceMarks.Count > pairsCount)
                record.AttendanceMarks.RemoveAt(record.AttendanceMarks.Count - 1);

            OnPropertyChanged(nameof(AttendanceRecords));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }





        private AttendanceRecord _currentAttendanceRecord;

        [JsonIgnore]
        public AttendanceRecord CurrentAttendanceRecord
        {
            get => _currentAttendanceRecord;
            set
            {
                _currentAttendanceRecord = value;
                OnPropertyChanged(nameof(CurrentAttendanceRecord));
            }
        }
    }

    public class AttendanceRecord : INotifyPropertyChanged
    {
        private Guid _studentId;
        private DateTime _date;
        private ObservableCollection<bool> _attendanceMarks;

        [JsonProperty]
        public Guid StudentId
        {
            get => _studentId;
            set
            {
                if (_studentId == value) return;
                _studentId = value;
                OnPropertyChanged(nameof(StudentId));
            }
        }

        [JsonProperty]
        public DateTime Date
        {
            get => _date;
            set
            {
                if (_date == value) return;
                _date = value;
                OnPropertyChanged(nameof(Date));
            }
        }

        [JsonProperty]
        public ObservableCollection<bool> AttendanceMarks
        {
            get => _attendanceMarks;
            set
            {
                _attendanceMarks = value;
                OnPropertyChanged(nameof(AttendanceMarks));
            }
        }

        public AttendanceRecord()
        {
            _attendanceMarks = new ObservableCollection<bool>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class EmailMapping : INotifyPropertyChanged
    {
        private Guid _studentId;
        private Student _student;

        public string Email { get; set; }

        public Guid StudentId
        {
            get => _studentId;
            set
            {
                if (_studentId != value)
                {
                    _studentId = value;
                    OnPropertyChanged(nameof(StudentId));
                }
            }
        }

        [JsonIgnore]
        public Student Student
        {
            get => _student;
            set
            {
                if (_student != value)
                {
                    _student = value;
                    StudentId = value?.Id ?? Guid.Empty;
                    OnPropertyChanged(nameof(Student));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LabWork
    {
        public Guid StudentId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string FilePath { get; set; }

        // Новое поле для идентификации уникальных работ
        public string MessageId { get; set; }
    }


}
