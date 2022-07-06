using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.SmartLedger;

namespace TgBot.Exchange.TgDialogs
{
    public class RegisterDialog:FormDialog 
    {
        const string FIELD_FIRST_NAME = "SpecifyName";
        const string FIELD_LAST_NAME = "SpecifiySurname";
        const string FIELD_SHORT_NAME = "ShortName";
        const String FIELD_GENDER= "Gender";
        const String FIELD_BIRTH_YEAR = "BirthYear";
        const String FIELD_BIRTH_MONTH = "BirthMonth";
        const String FIELD_BIRTH_DAY = "BirthDay";
        bool UpdateMode => new ExchangeDbService().GetUserProfile(from.Id.ToString()) != null;
        public RegisterDialog(ChatId chatId, User from) : base(chatId, from)
        {

        }
        public override string FirstField => FIELD_FIRST_NAME;

        public override FormDialogField GetFieldDef(string key)
        {

            switch (key)
            {
                case FIELD_FIRST_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Enter your name with your father's name (First name).Spell exactly like it is in your passport",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_LAST_NAME)
                    };
                case FIELD_LAST_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Enter your grand father's name (last name). Spell exactly like it is in your passport",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_SHORT_NAME)
                    };
                case FIELD_SHORT_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Enter a short form of your name",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_GENDER)
                    };
                case FIELD_GENDER:
                    return new FormDialogField
                    {
                        Prompt = $"What is your gender?",
                        FieldType = FieldType.Choices,
                        Choices = new[] { new FormFieldChoiceItem(MisUserProfile.GenderType.Male.ToString(),"Male")
                            ,new FormFieldChoiceItem(MisUserProfile.GenderType.Female.ToString(),"Female")},
                        NextField=d=>Task.FromResult(FIELD_BIRTH_YEAR)                        
                    };

                case FIELD_BIRTH_YEAR:
                    return new FormDialogField
                    {
                        Prompt = $"Enter year of birth.",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_BIRTH_MONTH),
                        ParseFunction = (t,d,c) => {
                            var res = new ParseResult();
                            int y;
                            if (int.TryParse(d, out y))
                            {
                                if (y < 1800 && y > DateTime.Now.Year)
                                    res.Error = "Invalid year. ";
                                else
                                    res.Data = y;

                            }
                            else
                                res.Error = "Invalid year.";
                            return Task.FromResult(res);
                            }
                    };

                case FIELD_BIRTH_MONTH:
                    var ch = DateTimeFormatInfo.CurrentInfo.MonthNames.Select(x => new FormFieldChoiceItem(x, x)).ToArray();
                    for (int i = 0; i < ch.Length; i++)
                        ch[i].Key = i.ToString();
                    return new FormDialogField
                    {
                        Prompt = $"Enter month of birth:",
                        FieldType = FieldType.Choices,
                        Choices = ch,
                        NextField = d => Task.FromResult(FIELD_BIRTH_DAY),
                        ParseFunction = (t, d, c) =>Task.FromResult(new ParseResult { Data = int.Parse(d) })
                    };
                case FIELD_BIRTH_DAY:
                    return new FormDialogField
                    {
                        Prompt = $"Enter day of birth.",
                        FieldType = FieldType.Text,
                        ParseFunction = (t, d, c) => {
                            var res = new ParseResult();
                            int day;
                            if (int.TryParse(d, out day))
                            {
                                var y = (int)base.FieldData[FIELD_BIRTH_YEAR].Val();
                                var m = (int)base.FieldData[FIELD_BIRTH_MONTH].Val();
                                var maxDay=DateTime.DaysInMonth(y, m);
                                if (day< 1&& day > maxDay)
                                    res.Error = "Invalid day.";
                                else
                                    res.Data = day;

                            }
                            else
                                res.Error = "Invalid day.";
                            return Task.FromResult(res);
                        }
                    };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new ExchangeDbService();
            String firstName=(String)FieldData[FIELD_FIRST_NAME].Val();
            String lastName= (String)FieldData[FIELD_LAST_NAME].Val();
            
            service.SetExchangeUserProfile(new Exchange.ExchangeUserProfile
            {
                UserId = from.Id.ToString(),
                FirstName = firstName,
                LastName=lastName,
                ShortName = (String)FieldData[FIELD_SHORT_NAME].Val(),
                DateOfBirth =new DateTime((int)FieldData[FIELD_BIRTH_YEAR].Val(),(int)FieldData[FIELD_BIRTH_MONTH].Val(),(int)FieldData[FIELD_BIRTH_DAY].Val()),
                Gender = Enum.Parse<ExchangeUserProfile.GenderType>((String)FieldData[FIELD_GENDER].Val())
                });
            await bot.SendTextMessageAsync(this.chatId, $"Profile saved",cancellationToken:cancellationToken);
            return DialogResult.Terminated;
        }
    }
}
