using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using TgBot.WFTGDB;
using Microsoft.EntityFrameworkCore;

namespace TgBot.TTDB
{
    [Table("TTPenalityRank")]
    public class PenalityRank
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Key]
        public int Rank { get; set; }
        public long PenalityAmount { get; set; }
        public String Description { get; set; }
    }
    [Table("TTDrivingLicense")]
    public class DrivingLicense
    {
        public Guid Id { get; set; }
        public String LicenseNo { get; set; }
        public bool Active { get; set; }
        public long ExpiryDate { get; set; }
    }

    [Table("TTTicket")]
    public class Ticket
    {
        public Guid Id { get; set; }
        public long Time {get;set;}
        public String DrivingLicenseNo { get; set; }
        public String PlateNo { get; set; }
        public int PenalityRankNo { get; set; }
        public String ReceiptNo { get; set; }
        public long Amount { get; set; }
        public string PoliceName { get; set; }
        public bool Paid { get; set; } = false;
        public long PaidTime { get; set; } = -1;
    }
    public class TTDBService: ServiceBase<WFDbContext>
    {
        long CalculateAmountInternal(WFDbContext db,int rank)
        {
            var res = db.PeanlityRanks.Where(x => x.Rank == rank);
            if (res.Any())
                return res.First().PenalityAmount;
            return -1;
        }
        Ticket RegisterTicketInternal(WFDbContext db,int rank,String dno, String pno)
        {
            var amount = CalculateAmountInternal(db, rank);
            if (amount < 1)
                throw new UserFriendlyError($"The rank {rank} is not valid");
            var license = db.DrivingLicenses.Where(x => x.LicenseNo.Equals(dno));
            //if (!license.Any())
            //    throw new UserFriendlyError($"Sorry the driving license no {dno} is not registerd in the system");
            //var l = license.First();
            //if (!l.Active)
            //    throw new UserFriendlyError($"Sorry the driving license no {dno} is inactive");

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                DrivingLicenseNo = dno,
                PenalityRankNo = rank,
                PlateNo = pno,
                Time = DateTime.Now.Ticks,
                ReceiptNo = new Random(1000000).Next().ToString("000000"),
                Amount = amount
            };
            db.Tickets.Add(ticket);
            return ticket;
        }

        void PayTicketInternal(WFDbContext db,Guid ticketId)
        {
            var ticket = GetTicket(db, ticketId);
            if (ticket == null)
                throw new UserFriendlyError("Ticket doesn't exist");
            if(ticket.Paid)
                throw new UserFriendlyError("Ticket allready paid");
            ticket.Paid = true;
            ticket.PaidTime = DateTime.Now.Ticks;
            db.Tickets.Update(ticket);
        }

        private Ticket GetTicket(WFDbContext db, Guid ticketId)
        {
            return db.Tickets.AsNoTracking().Where(x => x.Id == ticketId).FirstOrDefault();
        }

        public long CalculateAmount(int rank)
        {
            using (var db = new WFDbContext())
                return CalculateAmountInternal(db, rank);
        }
        public Ticket RegisterTicket(int rank, String dno, String pno)
        {
            return TransactReturn(db =>
            {
                return RegisterTicketInternal(db, rank, dno, pno);
            });
        }
        public void PayTicket(Guid ticketId)
        {
            TransactNoReturn(db =>
            {
                PayTicketInternal(db, ticketId);
            });
        }

    }
}
