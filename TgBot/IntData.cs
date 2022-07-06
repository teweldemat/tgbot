using System;

namespace TgBot
{
    public class IntData
    {
        static long UNITS_PER_ONE = 10000;
        static double ONE_UNIT = (double)1 / (double)UNITS_PER_ONE;
        public static double amountToDouble(long amount)
        {
            double ret = (double)amount / (double)UNITS_PER_ONE;
            return ret;
        }
        public static double roundToCents(long amount)
        {
            double ret = (double)amount / (double)UNITS_PER_ONE;
            return Math.Round(ret, 2);
        }
        public static double roundUpToCents(long amount)
        {
            double ret = (double)amount / (double)(UNITS_PER_ONE / 100);
            return Math.Ceiling(ret) / 100;
        }
        public static double roundDownToCentents(long amount)
        {
            double ret = amount / (UNITS_PER_ONE / 100);
            return Math.Floor(ret) / 100;
        }
        public static long toIntMoney(double amount)
        {
            return (long)Math.Round(amount / ONE_UNIT);
        }
        public static long toIntMoneyRoundUp(double amount)
        {
            return (long)Math.Ceiling(amount / ONE_UNIT);
        }
        public static long toIntMoneyRoundDown(double amount)
        {
            return (long)Math.Floor(amount / ONE_UNIT);
        }

        public static string toString(long amount,String zeroAmount=null,String negativeAmount=null)
        {
            if (amount == 0 && zeroAmount != null)
                return zeroAmount;
            if(amount<9 && negativeAmount!=null)
                return roundToCents(-amount).ToString(negativeAmount);
            return roundToCents(amount).ToString("#,#0.00");
        }
        public static String toDateString(long time)
        {
            if (time < 10000)
                return "";
            var date = new DateTime(time);
            return date.ToString("yyy-MM-dd HH:mm:ss");
        }
        public static long parseStringDate(String date)
        {
            return DateTime.Parse(date).Ticks;
        }
        public static long parseDotNetDate(DateTime date)
        {
            return date.Ticks;
        }

        internal static long fromString(string amount)
        {
            double d;
            if (!double.TryParse(amount, out d))
                throw new InvalidOperationException($"Incorrectly formated amount'{amount}'");
            return toIntMoney(d);
        }
        public static bool FloatAmountEqual(double x, double y)
        {
            return Math.Abs(x - y) < ONE_UNIT;
        }

        public static long toIntMoney(object maxContribution)
        {
            throw new NotImplementedException();
        }

        public static String toDateString(long time, string format)
        {
            return new DateTime(time).ToString(format);
        }
    }
}

