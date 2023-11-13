using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    [Table("Holiday")]
    public class Holiday
    {
        public Guid Id { get; set; }
        public long FromTime { get; set; }
        public long ToTime { get; set; }
        public String HolidayName { get; set; }
    }
    

    public enum DutyStationMode
    {
        OnSite,
        Remote,
        Off
    }
    public interface IDutyStationSechdule
    {
        DutyStationMode GetMode(TaskDbService service, String userId, long time);
        DutyTimeSpan GetOnCurrentOnDutySpan(TaskDbService service, String userId, long time);
    }
    public class DutyTimeSpan
    {
        public short StartHour { get; set; }

        internal long StartTime(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, this.StartHour, this.StartMinute, 0).Ticks;
        }

        public short StartMinute { get; set; }
        public short EndHour { get; set; }
        public short EndMinute { get; set; }
        public bool Remote { get; set; }
        internal bool InShift(int hour, int minute, long tollerance)
        {
            var t = hour * 60 + minute;
            var t1 = StartHour * 60 + StartMinute;
            var t2 = EndHour * 60 + EndMinute;
            return t1-tollerance <= t && t <= t2;
        }
        internal bool InShift(long t,long tollerance)
        {
            var dt = new DateTime(t);
            return InShift(dt.Hour, dt.Minute,tollerance);
        }

        internal bool InShift(long time, TimeSpan timeSpan)
        {
            throw new NotImplementedException();
        }
    }
    public class SimpleWorkingDayWeek:IDutyStationSechdule
    {        
        public class WorkingDay
        {
            public short Day { get; set; }
            public DutyTimeSpan MorningShift { get; set; }
            public DutyTimeSpan AfternonShift { get; set; }
            
        }
        public List<WorkingDay> WorkingDays { get; set; }
        static public SimpleWorkingDayWeek CreateDefaultFullTime(bool remote,bool includeHalfSaturady)
        {
            var ret = new SimpleWorkingDayWeek();
            ret.WorkingDays = new List<WorkingDay>(new[] {
                new WorkingDay{Day=(short)DayOfWeek.Monday,
                    MorningShift=new DutyTimeSpan{StartHour=8,StartMinute=30,EndHour=12,EndMinute=30,Remote=remote},
                    AfternonShift=new DutyTimeSpan{StartHour=13,StartMinute=30,EndHour=17,EndMinute=30,Remote=remote},
                },
                new WorkingDay{Day=(short)DayOfWeek.Tuesday,
                    MorningShift=new DutyTimeSpan{StartHour=8,StartMinute=30,EndHour=12,EndMinute=30,Remote=remote},
                    AfternonShift=new DutyTimeSpan{StartHour=13,StartMinute=30,EndHour=17,EndMinute=30,Remote=remote} },
                new WorkingDay{Day=(short)DayOfWeek.Wednesday,
                    MorningShift=new DutyTimeSpan{StartHour=8,StartMinute=30,EndHour=12,EndMinute=30,Remote=remote},
                    AfternonShift=new DutyTimeSpan{StartHour=13,StartMinute=30,EndHour=17,EndMinute=30,Remote=remote} },
                new WorkingDay{Day=(short)DayOfWeek.Thursday,
                    MorningShift=new DutyTimeSpan{StartHour=8,StartMinute=30,EndHour=12,EndMinute=30,Remote=remote},
                    AfternonShift=new DutyTimeSpan{StartHour=13,StartMinute=30,EndHour=17,EndMinute=30,Remote=remote} },
                new WorkingDay{Day=(short)DayOfWeek.Friday,
                    MorningShift=new DutyTimeSpan{StartHour=8,StartMinute=30,EndHour=12,EndMinute=30},
                    AfternonShift=new DutyTimeSpan{StartHour=13,StartMinute=30,EndHour=17,EndMinute=30,Remote=remote} },
            });
            if (includeHalfSaturady)
                ret.WorkingDays.Add(new WorkingDay
                {
                    Day = (short)DayOfWeek.Saturday,
                    MorningShift = new DutyTimeSpan { StartHour = 8, StartMinute = 30, EndHour = 12, EndMinute = 30, Remote = remote },
                    AfternonShift = null
                });
            return ret;
        }
        public DutyStationMode GetMode(TaskDbService service, String userId,long time)
        {
            var sh = GetOnCurrentOnDutySpan(service, userId,time);
            if (sh == null)
                return DutyStationMode.Off;
            if (sh.Remote)
                return DutyStationMode.Remote;
            return DutyStationMode.OnSite;
        }

        public DutyTimeSpan GetOnCurrentOnDutySpan(TaskDbService service, String userId,long time)
        {
            var holiday=service.GetHoliday(time);
            if (holiday!=null)
                return null;
            var dateTime = new DateTime(time);
            var wd = WorkingDays.Where(x => (DayOfWeek)x.Day == dateTime.DayOfWeek).FirstOrDefault();
            if (wd == null)
                return null;
            foreach (var sh in new[] { wd.MorningShift, wd.AfternonShift })
                if (sh != null && sh.InShift(dateTime.Hour, dateTime.Minute))
                {
                    var ex = service.GetDSException(userId, time);
                    if (ex != null)
                    {
                        if (ex.remoteWork) //got remote work excetion
                        {
                            sh.Remote = true;
                        }
                        else
                            return null; //got leave exception
                    }
                    return sh;
                }
            return null;
        }
    }
}
