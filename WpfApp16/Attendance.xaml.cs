using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace StudentAttendanceApp
{
    public partial class Attendance : Window
    {
        public Student Student { get; set; }

        public Attendance(Student student)
        {
            InitializeComponent();
            Student = student;
            DataContext = this;
        }
    }
}