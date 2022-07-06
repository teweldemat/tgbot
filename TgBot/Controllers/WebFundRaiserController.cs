using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TgBot.WFDB;

namespace TgBot.Controllers
{
    public class APIReturn
    {
        public string error { get; set; } = null;
        public object res { get; set; } = null;
    }
    
    public class WebFundRaiserController:Controller
    {
        private const string SESSION_COOKY = "WeFundSession";

        IActionResult Error(Exception ex)
        {
            return Json(new APIReturn { error = ex.Message, 
                res = new { ex.Message, ex.StackTrace } });
        }
        static string hashPassword(string password)
        {
            byte[] hash = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.Unicode.GetBytes(password));
            return Convert.ToBase64String(hash);
        }
        UserSession assertSession()
        {
            var sid = base.HttpContext.Request.Cookies[SESSION_COOKY];
            if (string.IsNullOrEmpty(sid))
                throw new Exception("Invalid session");
            var session = new WFDB.WFDBService().GetUserSession(Guid.Parse(sid));
            if (session == null || !session.Active)
                throw new Exception("Invalid session");
            return session;
        }
        UserSession GetSession()
        {
            var sid = base.HttpContext.Request.Cookies[SESSION_COOKY];
            if (string.IsNullOrEmpty(sid))
                return null;
            var session = new WFDB.WFDBService().GetUserSession(Guid.Parse(sid));
            if (session == null || !session.Active)
                return null;
            return session;
        }

        public class UserModel
        {
            public Guid Id { get; set; }
            public String Email { get; set; }
            public String Name { get; set; }
            public String Password { get; set; }
        }
        [HttpPost]
        [Route("/wfweb/user/")]
        public IActionResult RegisterUser([FromBody] UserModel user)
        {
            try
            {
                var servce = new WFDB.WFDBService();
                user.Password = hashPassword(user.Password);
                return Json(new APIReturn
                {
                    res = servce.RegiserWebUser(new WebUser
                    {
                        Email = user.Email,
                        Name = user.Name,
                        Password = user.Password
                    })
                }); ;
            }
            catch(Exception ex)
            {
                return Error(ex);
            }
        }
        public class LoginModel
        {
            public String Email { get; set; }
            public String Password { get; set; }
        }
        [HttpPost]
        [Route("/wfweb/login/")]
        public IActionResult Login([FromBody] LoginModel user)
        {
            try
            {
                var servce = new WFDB.WFDBService();
                user.Password = hashPassword(user.Password);
                var session = servce.Login(user.Email, user.Password);
                base.HttpContext.Response.Cookies.Append(SESSION_COOKY, session.Id.ToString());
                return Json(new APIReturn
                {
                    res = session.Id
                });
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpPost]
        [Route("/wfweb/picture/")]
        public IActionResult GetPicture(String id)
        {
            try
            {
                var service = new WFDB.WFDBService();
                var sessionpic = service.GetSessionPicture(Guid.Parse(id));
                byte[] data = null;
                String type = null;
                if(sessionpic!=null )
                {
                    data = sessionpic.Data;
                    type = sessionpic.MimeType;
                }
                else
                {
                    var pi = service.GetFundRaiserPicture(Guid.Parse(id));
                    if(pi!=null)
                    {
                        data = pi.Picture;
                        type = pi.PictureMIME;
                    }
                }
                if (data == null)
                    throw new Exception("Picture doesn't exist");
                return base.File(data, type);
            }
            catch (Exception ex)
            {
                Program.LogException("Error to call: GetPicture", ex);
                return StatusCode(500);

            }
        }

        [HttpPost]
        [Route("/wfweb/picture/")]
        public IActionResult UploadPictures()
        {
            try
            {
                assertSession();
                var service = new WFDBService();
                var guid = new List<Guid>();
                if (base.HttpContext.Request.Form.Files.Count > 0)
                {
                    var docfiles = new List<string>();
                    foreach (var postedFile in base.HttpContext.Request.Form.Files)
                    {
                        if (!(postedFile.ContentType.Equals("image/jpeg")
                             || postedFile.ContentType.Equals("image/png"))
                             )
                            throw new Exception("Unspoorted image format");
                    }

                    foreach (var postedFile in base.HttpContext.Request.Form.Files)
                    {

                        byte[] buf;
                        using (var ms = new System.IO.MemoryStream())
                        {
                            postedFile.CopyTo(ms);
                            ms.Seek(0, System.IO.SeekOrigin.Begin);
                            buf = new byte[ms.Length];
                            ms.Read(buf, 0, buf.Length);
                        }
                        var g = service.AddSessionPicture(postedFile.ContentType, buf);
                        guid.Add(g);
                    }
                }
                return Json(new APIReturn { res = guid });
            }
            catch (Exception ex)
            {
                Program.LogException("Error to call: UploadPicture", ex);
                return StatusCode(500);

            }
        }
        
        public class FundRaiserModel
        {
            public String Title { get; set; }
            public String Description { get; set; }
            public bool HasTarget { get; set; }
            public double TargetAmount { get; set; }
            public List<Guid> Pictures { get; set; }
        }
        [HttpPost]
        [Route("/wfweb/fundraiser/")]
        public IActionResult CreateFundRaiser([FromBody] FundRaiserModel fr )
        {
            try
            {
                var session = assertSession();
                var service = new WFDB.WFDBService();
                var dbfr = new WFDB.FundRaiser
                {
                    AgentID = service.GetAgentByChannel(FundRaisingChannel.CHANNEL_WEB, session.UserID.ToString()).Id,
                    ChannelID = FundRaisingChannel.CHANNEL_WEB,
                    ShortName = fr.Title,
                    ShortDescription = fr.Description,
                    TargetAmount = fr.HasTarget ? IntData.toIntMoney(fr.TargetAmount) : -1,
                    StartTime = DateTime.Now.Ticks,
                };
                var frid = service.RegisterFundRaiser(dbfr, fr.Pictures);

                return Json(new APIReturn
                {
                    res = session.Id
                });
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }
        
        public class ConstributionModel
        {
            public String Name { get; set; }
            public bool Anonymous { get; set; }
            public double Amount { get; set; }
            public Guid FundRaiserId { get; set; }
        }
        [HttpGet]
        [Route("/wfweb/fundraiser/contribution")]

        public IActionResult RegisterContribution([FromBody] ConstributionModel cont)
        {
            try
            {
                var session = GetSession();
                if(string.IsNullOrEmpty(cont.Name) && session==null)
                {
                    throw new Exception("Provide name or login");
                }
                var service = new WFDB.WFDBService();
                Guid? agentID = null;
                if (session != null)
                    agentID = service.GetAgentByChannel(FundRaisingChannel.CHANNEL_WEB, session.UserID.ToString()).Id;
                var wbc=service.CreatePaymentCodeForContribution(new Contribution
                {
                    AgentID = agentID,
                    Anonymous=cont.Anonymous,
                    Alias=cont.Name==null?cont.Name:null,
                    Amount=IntData.toIntMoney(cont.Amount),
                    ChannelId= FundRaisingChannel.CHANNEL_WEB,
                    ContributionTime=DateTime.Now.Ticks,
                    FundRaiserId=cont.FundRaiserId,
                    RegisteredAgent=agentID!=null
          
                });
                return Json(new APIReturn
                {
                    res = wbc.Result
                });
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpGet]
        [Route("/wfweb/fundraiser/payment")]
        public IActionResult CheckPayment(String payment_code)
        {
            try
            {
                var service = new WFDB.WFDBService();
                var paid = service.CheckContributionPayment(payment_code);
                return Json(new APIReturn
                {
                    res = paid.Result
                });
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }
    }
}
