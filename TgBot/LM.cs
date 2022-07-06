using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TgBot.SmartLedger;

namespace TgBot
{
    public class LM
    {
        //public virtual String 
        public virtual string Nothing_to_cancel => "Nothing to cancel.";
        public virtual string Ok_canceled => "Ok, canceled.";
        public virtual string Thank_you_We_have_received_the_payment => "Thank you. We have received the payment";
        public virtual string Someone => "Someone";
        public virtual string Contribute => "Contribute";
        public virtual string No_contributions_yet__Work_on_the_promotion => "No contributions yet. Work on the promotion.";
        public virtual string How_much_do_you_want_to_contribute => "How much do you want to contribute?";
        public virtual string Yes => "Yes.";
        public virtual string No_hide_my_name => "No, hide my name.";
        public virtual string Can_we_show_your_name_in_the_contributers_list => "Can we show your name in the contributers list?";
        public virtual string Enter_a_small_message_to_inlcude_with_your_contribution => "Enter a small message to inlcude with your contribution.";
        public virtual string Thanks_your_name_will_be_shown_in_the_contribution_list => "Thanks, your name will be shown in the contribution list";
        public virtual string Alright_we_will_respect_your_privacy__We_will_not_show_your_name => "Alright, we will respect your privacy. We will not show your name.";

        public virtual String Use_the_following_Code_to_pay_with_WeBirr => "Use the following Code to pay with WeBirr";

        public virtual String How_to_pay_with_WeBirr => "How to pay with WeBirr?";

        public virtual string Minimum_anount_is_1_Birr => "Minimum anount is 1 Birr";

        public virtual string Yes_please => "Yes please.";

        public virtual string No_thanks => "No thanks.";

        public virtual string Do_you_want_to_include_a_small_message_with_your_contribution => "Do you want to include a small message with your contribution?";

        public virtual string Save_it_looks_right => "Save, it looks right.";

        public virtual string Cancel => "Cancel.";

        public virtual string Change_title => "Change title.";

        public string Change_description => "Change description.";

        public string Set_Target => "Set Target";

        public string Change_target => "Change target.";

        public string Change_picture => "Change picture.";
        public String Set_Picture => "Set Picture";

        public string No_traget_set => "No target set";

        public string Enter_a_short_title_for_your_fundraiser => "Enter a short title for your fundraiser";

        public string Alright_canceled => "Alright, canceled!";

        public string Congradulations_your_fundraiser_created =>
            "Congradulations your fundraiser is succesfully created and is ready to accept contributions.\n Put some effort towards promotting it. Good luck!";

        public string Enter_fundraiser_target => "Enter fundraiser target";

        public string Upload_a_picture_for_your_fundraiser => "Upload a picture for your fundraiser.";

        public string Enter_a_new_title_for_your_fundraiser => "Enter a new title for your fundraiser";

        public string Enter_a_description_of_your_fundraiser => "Enter a description of your fundraiser";

        public string Enter_target_for_your_fund_raiser => "Enter target for your fund raiser";

        public string Remove_target => "Remove the target.";

        public string What_do_you_want_to_do_with_the_fundraiser_target => "What do you want to do with the fundraiser target?";
        public string Upload_picture_for_your_fund_raiser => "Upload picture for your fund raiser";

        public string Remove_the_picture => "Remove the picture.";

        public string What_do_you_want_to_do_with_the_fundraiser_picture => "What do you want to do with the fundraiser picture?";

        public string Do_you_want_to_set_a_picture_for_your_funraiser__it_is_highly_recommended
            => "Do you want to set a picture for your funraiser, it is highly recommended?";

        public string Excellent_choice__Upload_a_picture_for_your_fundraiser
            => "Excellent choice. Upload a picture for your fundraiser.";

        public string Alright_you_can_come_back_anytime_and_set_a_picture
            => "Alright, you can come back anytime and set a picture.";

        public string Please_enter_a_valid_amount_that_is_at_least_1_Birr
            => "Please enter a valid amount that is at least 1 Birr";

        public virtual string Contributed_to_fundraiser(String name, String frName, String amount)
        {
            return $"{name} contributed {amount} Birr to {frName}";
        }

        public virtual string Show_more_n_records(int nrec)
        {
            return $"Show {nrec} more";
        }

        public virtual string Target_amount(string amount)
        {
            return $"Target:{amount} Birr";
        }

        public virtual string payment_verb(SmartLedger.Payment payment, bool action = false, bool pastAction = false, bool capitalize = false)
        {
            String ret;
            if (pastAction)
                ret = payment.IsDeposit ? "deposited" : (payment.IsTransferTransaction ? "transfered" : "paid");
            else if (action)
                ret = payment.IsDeposit ? "deposit" : (payment.IsTransferTransaction ? "transfer" : "pay");
            else
                ret = payment.IsDeposit ? "deposit" : (payment.IsTransferTransaction ? "transfer" : "payment");
            if (capitalize)
            {
                ret = Char.ToUpper(ret[0]) + ret.Substring(1);
            }
            return ret;
        }

        public virtual string payment_request_subject_question(PaymentType paymentType)
        {
            switch (paymentType)
            {
                case PaymentType.Deposit:
                    return "Who is paying this?";
                case PaymentType.Transfer:
                    return "Which account do you want transferring to?";
                case PaymentType.Payment:
                    return "Who will be paid?";
            }
            return "Who/what is the payment subject?";
        }

        // public virtual string payment_action(SmartLedger.Payment payment, String sourceAccount,String destinationAccount, String amount,bool capitalize = true,bool instruction=false)
        public virtual string payment_action(SmartLedger.Payment payment, string cashAccountName, string trasnferToAccount, bool capitalize = true, bool instruction = false)
        {
            String ret;
            var amount = IntData.toString(payment.PositiveAmount);
            String cashSource;
            String cashDest;
            if (payment.IsDeposit)
            {
                cashSource = payment.ToPayTo;
                cashDest = cashAccountName;
            }
            else if (payment.IsTransferTransaction)
            {
                cashSource = cashAccountName;
                cashDest = trasnferToAccount;
            }
            else
            {
                cashSource = cashAccountName;
                cashDest = payment.ToPayTo;
            }
            if (instruction)
            {
                if (payment.IsDeposit)
                    ret = $"deposit {amount} to {cashDest} from {cashSource}";
                else if (payment.IsTransferTransaction)
                    ret = $"transfer {amount} to {cashDest} from {cashSource}";
                else
                    ret = $"pay {amount} to {cashDest} from {cashSource}";

            }
            else
            {
                if (payment.IsDeposit)
                    ret = $"deposited {amount} to {cashDest} from {cashSource}";
                else if (payment.IsTransferTransaction)
                    ret = $"transfered {amount} to {cashDest} from {cashSource}";
                else
                    ret = $"paid {amount} to {cashDest} from {cashSource}";
            }

            if (capitalize)
            {
                ret = Char.ToUpper(ret[0]) + ret.Substring(1);
            }
            return ret;
        }

        internal String _transform_case(string txt, bool sentenceCase)
        {
            if (string.IsNullOrEmpty(txt))
                return txt;
            if (sentenceCase)
                return char.ToUpper(txt[0]) + txt.Substring(1);
            return txt;

        }
        internal String _pronoun(MisUserProfile.GenderType gender, bool sentenceCase = false)
        {
            return _transform_case(
                new Func<String>(() =>
                {
                    switch (gender)
                    {
                        case MisUserProfile.GenderType.Male:
                            return "he";
                        case MisUserProfile.GenderType.Female:
                            return "she";
                        default:
                            return "they";
                    }
                })(), sentenceCase);
        }
        internal String _pronoun_possessive(MisUserProfile.GenderType gender, bool sentenceCase = false)
        {
            return _transform_case(
                new Func<String>(() =>
                {
                    switch (gender)
                    {
                        case MisUserProfile.GenderType.Male:
                            return "his";
                        case MisUserProfile.GenderType.Female:
                            return "her";
                        default:
                            return "their";
                    }
                })(), sentenceCase);
        }
    }
}
