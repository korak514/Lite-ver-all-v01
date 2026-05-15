using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_LoginForm.Models
{
    public class MonthlyValidationItem
    {
        public string CheckName { get; set; }
        public string Detail { get; set; }
        public string Status { get; set; }
        public string StatusColor => Status == "Good" ? "#2ECC71" : "#E74C3C";
    }

    public enum ShiftStatus { Neutral, Good, Bad }

    public class CalendarDayModel : INotifyPropertyChanged
    {
        private ShiftStatus _dayStatus = ShiftStatus.Neutral;
        private ShiftStatus _nightStatus = ShiftStatus.Neutral;
        public int Day { get; set; }

        public ShiftStatus DayStatus
        {
            get => _dayStatus;
            set
            {
                if (_dayStatus != value)
                {
                    _dayStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LeftColor));
                    OnPropertyChanged(nameof(RightColor));
                    OnPropertyChanged(nameof(IsColored));
                }
            }
        }

        public ShiftStatus NightStatus
        {
            get => _nightStatus;
            set
            {
                if (_nightStatus != value)
                {
                    _nightStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LeftColor));
                    OnPropertyChanged(nameof(RightColor));
                    OnPropertyChanged(nameof(IsColored));
                }
            }
        }

        public bool IsCurrentDay { get; set; }
        public bool IsColored => DayStatus != ShiftStatus.Neutral || NightStatus != ShiftStatus.Neutral;

        public string LeftColor => DayStatus == ShiftStatus.Bad ? "#FFC047" : DayStatus == ShiftStatus.Good ? "#2ECC71" : "#666666";
        public string RightColor => NightStatus == ShiftStatus.Bad ? "#2980B9" : NightStatus == ShiftStatus.Good ? "#2ECC71" : "#666666";
        public string BackgroundColor => IsCurrentDay ? "#333366" : "Transparent";

        public bool IsVisible { get; set; } = true;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
