using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace TgBot
{
    public class SMSService
    {
        public async Task<(bool, SMSApiResponse)> SendMessageAsync(string message, string recepients)
        {
             var valid = false;
		    (valid, recepients) = NormalizeEtTelNum(recepients);
		     if (!valid) return (false, null);

            // production system !!
            var uid = "WeBirrAppUser";
            var apikey = "1e93ab8aa3ac49c6ee7dc731e58eb4d52d094f2d16cf798a3bc421dd2041eddc";
            string url = "https://api.africastalking.com/version1/messaging";
            string senderId = "WeBirr"; 

            var data = new Dictionary<string, string> {
                                { "username", uid },
		                        { "from", senderId }, 
		                        { "to", recepients },
                                { "message", message },
                            };

            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apiKey", apikey);

            var resp = client.PostAsync(url, new FormUrlEncodedContent(data));
            var body = await resp.Result.Content.ReadAsStringAsync();
            //Console.WriteLine( $"HTTP Response status: {resp.Result.StatusCode}  {resp.Result.IsSuccessStatusCode}");
            if (resp.Result.IsSuccessStatusCode)
            {
                var smsResp = JsonSerializer.Deserialize<SMSApiResponse>(body);
                return (true, smsResp);

            }

            return (false, null);

        }

      public (bool, string) NormalizeEtTelNum(string input)
	  {
		if (string.IsNullOrWhiteSpace(input))
			return (false, input);

		string[] telNums = input.Split(',');

		for (int i = 0; i < telNums.Length; i++)
		{
			var (valid, tel) = _normalizeSingleTel(telNums[i]);
			if (!valid)
				return (false, input);
			telNums[i] = tel;
		}

		return (true, string.Join(',', telNums));

	  }

	   // Normalizes one Tel. Number
	   (bool, string) _normalizeSingleTel(string input)
	   {
		if (string.IsNullOrWhiteSpace(input))
			return (false, input);

		var tel = input.Replace(" ", string.Empty).Replace("-", string.Empty);

		if (!(tel.StartsWith("+2519") || tel.StartsWith("09") || tel.StartsWith("9")))
			return (false, input);

		if (tel.StartsWith("9"))
		{
			tel = "0" + tel;
		}

		if (tel.StartsWith("09"))
		{
			tel = "+251" + tel.TrimStart('0');
		}

		if (tel.Length != 13)
			return (false, input);

		for (int i = 5; i < 13; i++)
		{
			if (!char.IsDigit(tel[i]))
				return (false, input);
		}

		 return (true, tel);
	   }

    }

    public class SMSApiResponse
    {
        public SMSMessageData SMSMessageData { get; set; }
    }

    public class SMSMessageData
    {
        public string Message { get; set; }
        public List<SMSRecipient> Recipients { get; set; }
    }

    public class SMSRecipient
    {
        public int statusCode { get; set; }
        public string number { get; set; }
        public string status { get; set; }
        public string cost { get; set; }
        public string messageId { get; set; }
    }


}