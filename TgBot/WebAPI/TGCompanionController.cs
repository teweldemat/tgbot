using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TgBot.WebAPI
{
    [Route("tg")]
    public class TGCompanionController: Controller
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Content("There you go!");
        }
        [HttpGet("picprv")]
        public IActionResult FRPPreviewicture(String tgUserID)
        {
            var service = new WFTGDB.WFTGDBService();
            var state = service.GetUserState(tgUserID);
            if (state == null || state.Data == null || state.Data.Picture!=null && !state.Data.Picture.HasPicture)
                return StatusCode(404);
            var ms = new System.IO.MemoryStream(state.Data.Picture.Picture);
            return File(ms, state.Data.Picture.PictureMIME);

        }
        [HttpGet("fr")]
        public IActionResult GetFRPage(string frid)
        {
            return View("/Views/TG/FRPage.cshtml");
        }
        [HttpGet("pic")]
        public IActionResult FRPicture(String frid)
        {
            try
            {
                var service = new WFDB.WFDBService();
                var pics= service.GetFundRaiserPictures(Guid.Parse(frid));
                if (pics.Count==0)
                    return StatusCode(404);
                var pic = pics[0];
                var ms = new System.IO.MemoryStream(pic.Picture);
                return File(ms, pic.PictureMIME );
            }
            catch (Exception ex)
            {
                return StatusCode(505, ex.Message);
            }
        }
    }
}
