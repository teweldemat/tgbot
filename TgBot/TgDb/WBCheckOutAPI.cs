using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TgBot.TgDb
{
    partial class TgDbService
    {
        public static class WBCheckOutAPI
        {
            static String endPoint;
            static String apiKey;
            static String merchantID;
            static WBCheckOutAPI()
            {
                endPoint = Program.GetWBCConf("EndPoint");
                apiKey= Program.GetWBCConf("APIKey");
                merchantID= Program.GetWBCConf("MerchantID");
            }
            public class EInvoice
            {
                public String wbcCode { get; set; }
                public String billReference { get; set; }
                public string customerCode { get; set; }
                public String customerName { get; set; }
                public String time { get; set; }
                public String description { get; set; }
                public string detailHtml { get; set; }
                public string amount { get; set; }
                public int serialNo { get; set; }
                public String merchantID { get; set; }

                public int paymentStatus { get; set; }
                public string paymentReference { get; set; }
                public int billStatus { get; set; }
                public string merchantBankAccount { get; set; }
            }
            public class APIReturn<Type> where Type:class
            {
                public string error = null;
                public Type res = null;
            }
            class PaymentStatus
            {
                public int status { get; set; }
                public class PaymentStatusData
                {
                    public int id { get; set; }
                    public String paymentReference { get; set; }
                    public bool confirmed { get; set; }
                    public String confirmedTime { get; set; }
                    public String bankID { get; set; }
                }
            }
            public static async Task<String> CheckOut(String customerName, String customerCode, String description, long amount)
            {
                var invoice = new EInvoice
                {
                    amount=IntData.amountToDouble(amount).ToString(),
                    description=description,
                    customerCode= string.IsNullOrEmpty(customerCode)?customerName:customerCode,
                    customerName=customerName,
                    time=TGBot.NowDt().ToString("yyyy-MM-dd HH:mm:ss"),
                    merchantID=merchantID
                };
                using(var httpClient = new HttpClient())
                {
                    var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(invoice), Encoding.UTF8, "application/json");
                    using (var response = await httpClient.PostAsync($"{endPoint}postbill?api_key={apiKey}",content))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new Exception("HTTP ERROR CODE:" + response.StatusCode);
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        var res= Newtonsoft.Json.JsonConvert.DeserializeObject<APIReturn<String>>(apiResponse);
                        if (res.error != null)
                            throw new Exception("Error calling checkout:" + res.error);
                        return res.res;
                    }
                }
            }

            internal static async Task<bool> CheckPayment(string wbcCode)
            {
                using(var httpClient = new HttpClient())
                {
                    var url = $"{endPoint}GetPaymentStatus?api_key={apiKey}&wbc_code={System.Web.HttpUtility.UrlEncode(wbcCode)}";

                    using (var response = await httpClient.GetAsync(url))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        var res = Newtonsoft.Json.JsonConvert.DeserializeObject<APIReturn<PaymentStatus>>(apiResponse);
                        if (res.error != null)
                            throw new Exception("Error calling CheckPayment:" + res.error);                        
                        return res.res!=null && res.res.status==2;
                    }
                }
            }

            internal static async Task CancelCheckOut(string wbcCode)
            {
                using (var httpClient = new HttpClient())
                {
                    var url = $"{endPoint}CancelCheckout?api_key={apiKey}&wbc_code={System.Web.HttpUtility.UrlEncode(wbcCode)}";

                    using (var response = await httpClient.DeleteAsync(url))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        var res = Newtonsoft.Json.JsonConvert.DeserializeObject<APIReturn<String>>(apiResponse);
                        if (res.error != null)
                            throw new Exception("Error calling CheckPayment:" + res.error);
                    }
                }
            }
        }

        internal object GetUserState(object creator)
        {
            throw new NotImplementedException();
        }
    }
}
