using System;
using System.Collections.Generic;

namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminCalendarVm
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public List<AdminEventRowVm> Events { get; set; } = new();

        public DateTime FirstDay => new DateTime(Year, Month, 1);
        public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

        public int FirstDayOfWeekIndex
        {
            get
            {
                var day = (int)FirstDay.DayOfWeek;
                return day == 0 ? 6 : day - 1;
            }
        }
    }
}