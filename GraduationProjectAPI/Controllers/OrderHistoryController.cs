﻿using AutoMapper;
using GraduationProjectAPI.BL;
using GraduationProjectAPI.BL.Interfaces;
using GraduationProjectAPI.BL.VM;
using GraduationProjectAPI.DAL.Database;
using GraduationProjectAPI.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace GraduationProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderHistoryController : ControllerBase
    {
        private readonly IOrderHistories orderHistories;
        private readonly IMapper mapper;
        private readonly IMedicine medicine;
        private readonly IPrescription prescription;
        private readonly DataContext db;
        public OrderHistoryController(DataContext db,IOrderHistories orderHistories,IMapper mapper,IMedicine medicine,IPrescription prescription)
        {
            this.orderHistories = orderHistories;
            this.mapper = mapper;
            this.medicine = medicine;
            this.prescription = prescription;
            this.db = db;
        }

        [HttpGet]
        [Route("GetAll")]
        public CustomResponse<IEnumerable<OrderHistoryVM>> GetAll()
        {
            var data = orderHistories.GetAll();
            var result = mapper.Map<IEnumerable<OrderHistoryVM>>(data);
            return new CustomResponse<IEnumerable<OrderHistoryVM>> { StatusCode = 200, Data = result, Message = "Data Retreived Successfully" };
        }

        [HttpGet]
        [Route("GetById/{PrescriptionId}/{PharmacistId}")]
        public CustomResponse<OrderHistoryVM> GetById(int PrescriptionId, int PharmacistId)
        {
            var data = orderHistories.GetById(PrescriptionId,PharmacistId);
            if (data is not null)
            {
                var result = mapper.Map<OrderHistoryVM>(data);
                return new CustomResponse<OrderHistoryVM> { StatusCode = 200, Data = result, Message = "Data Retreived Successfully" };

            }
            else
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = "data Not Found" };

            }
        }

        [HttpPost]
        [Route("create")]
        public CustomResponse<OrderHistoryVM> Create(OrderHistoryVM orderHistory)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var data = mapper.Map<OrderHistory>(orderHistory);
                    orderHistories.Add(data);
                    return new CustomResponse<OrderHistoryVM> { StatusCode = 200, Data = orderHistory, Message = "OrderHistory Added Successfully" };

                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage)
                                        .ToList();
                    var message = "";
               
                    message = string.Join(",", errors);
                    return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 500, Data = null, Message = ex.Message };

            }
        }


        [HttpPut]
        [Route("Update")]
        public CustomResponse<OrderHistoryVM> Update(OrderHistoryVM orderHistory)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var data = mapper.Map<OrderHistory>(orderHistory);
                    orderHistories.Update(data);
                    return new CustomResponse<OrderHistoryVM> { StatusCode = 200, Data = orderHistory, Message = "OrderHistory Updated Successfully" };

                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage)
                                        .ToList();
                    var message = string.Join(",", errors);
                    return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 500, Data = null, Message = ex.Message };

            }
        }

        [HttpDelete]
        [Route("Delete/{PrescriptionId}/{PharmacistId}")]
        public CustomResponse<OrderHistoryVM> Delete(int PrescriptionId, int PharmacistId)
        {
            var data = orderHistories.GetById(PrescriptionId, PharmacistId);
            var result = mapper.Map<OrderHistoryVM>(data);
            if (data is not null)
            {
                orderHistories.Delete(PrescriptionId, PharmacistId);
                return new CustomResponse<OrderHistoryVM> { StatusCode = 200, Data = result, Message = "OrderHistory deleted successfully" };

            }
            else
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 404, Data = null, Message = "OrderHistory Not Found" };

            }
        }


        [HttpPost]
        [Route("Submit")]
        public CustomResponse<OrderHistoryVM> Submit(int pharmacistid, int Prescriptionid)
        {
            var pres = prescription.GetByIDWithSPecificRelatedData(Prescriptionid);

            if (pres == null || pres.medicineOfPrescriptions == null)
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = "Invalid prescription or no medicines found." };
            }

            List<string> errorMessages = new List<string>();

            foreach (var item in pres.medicineOfPrescriptions)
            {
                var medicine = item.Medicine;
                if (medicine == null)
                {
                    continue; // Skip this medicine if it's not found
                }

                var differenceInDays = 0;
                var date = DateTime.Now;
                var medicineExpDate = medicine.ExpirationDate;
                TimeSpan difference = medicineExpDate - date;
                differenceInDays = (int)difference.TotalDays;

                if (differenceInDays <= 0)
                {
                    errorMessages.Add($"{medicine.Name} is expired.");
                }
                else if (medicine.NumberInStock <= 0)
                {
                    errorMessages.Add($"{medicine.Name} is out of stock.");
                }
            }

            if (errorMessages.Count > 0)
            {
                string errorMessage = string.Join(" ", errorMessages);
                return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = errorMessage };
            }

            // Check if an order history with the same PrescriptionId already exists
            var existingOrderHistory = orderHistories.FirstOrDefault(o => o.PrescriptionId == pres.Id);

            if (existingOrderHistory != null)
            {
                return new CustomResponse<OrderHistoryVM> { StatusCode = 400, Data = null, Message = "An order history with the same PrescriptionId already exists." };
            }

            // If there are no errors and no existing order history, proceed with order submission
            var record = new OrderHistoryVM
            {
                PatientId = pres.PatientID,
                PharmacistId = pharmacistid,
                PrescriptionId = pres.Id
            };

            var data = mapper.Map<OrderHistory>(record);
            orderHistories.Add(data);

            foreach (var item in pres.medicineOfPrescriptions)
            {
                medicine.decrementQuanity(item.Medicine.Id, 1);
            }

            db.shelfNumberStatus.RemoveRange(db.shelfNumberStatus.Select(a => a));

            foreach (var item in pres.medicineOfPrescriptions)
            {
                var shelfstatus = new ShelfNumberStatus
                {
                    shelfNumber = item.Medicine.ShelFNumber,
                    status = "Green"
                };
                db.shelfNumberStatus.Add(shelfstatus);
            }

            db.SaveChanges();

            return new CustomResponse<OrderHistoryVM> { StatusCode = 200, Data = null, Message = "Prescription submitted successfully." };
        }

    }
}

