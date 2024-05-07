﻿using Azure;
using GraduationProjectAPI.BL;
using GraduationProjectAPI.BL.Interfaces;
using GraduationProjectAPI.BL.Services;
using GraduationProjectAPI.BL.VM;
using GraduationProjectAPI.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Identity.Client.AppConfig;
using System.Diagnostics.Eventing.Reader;
using System.Text;

namespace GraduationProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;
        private readonly IDoctor doctor;
        private readonly IPharmacist pharmacist;

        public AccountController(UserManager<IdentityUser> userManager,SignInManager<IdentityUser> signInManager, IDoctor doctor,IPharmacist pharmacist)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.doctor = doctor;
            this.pharmacist = pharmacist;
        }

        [HttpPost]
        [Route("DoctorLogin")]

        public async Task<CustomResponse<IdentityUser>> DoctorLogin(LoginVM login)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await userManager.FindByEmailAsync(login.Email);
                    if (user is not null)
                    {
                        var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, false, false);
                        if (result.Succeeded)
                        {

                            if (await userManager.IsInRoleAsync(user, "Doctor"))
                            {
                                return new CustomResponse<IdentityUser> { StatusCode = 200, Data = user, Message = "OK" };

                            }
                            else
                            {
                                return new CustomResponse<IdentityUser> { StatusCode = 401, Data = user, Message = "Not A Doctor Account" };

                            }



                        }
                        else
                        {

                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = "Invalid userName or Password Attempt" };

                        }
                    }
                    else
                    {
                        return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = "Invalid userName or Password Attempt" };
                    }

                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                        .Select(e => e.ErrorMessage)
                                                        .ToList();
                    var message = string.Join(",", errors);

                    return new CustomResponse<IdentityUser> { StatusCode = 403, Data = null, Message = message };
                }
              

            }
            catch (Exception ex)
            {
                return new CustomResponse<IdentityUser> { StatusCode = 500, Data = null, Message = ex.Message};

            }
        }

        [HttpPost]
        [Route("DoctorRegister")]
        public async Task<CustomResponse<IdentityUser>> DoctorRegister(DoctorRegisterVM register)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = new IdentityUser
                    {
                        Email = register.Email,
                        UserName = register.Email,
                    };

                    var result = await userManager.CreateAsync(user, register.Password);
                    if (result.Succeeded)
                    {
                        var roleResult = await userManager.AddToRoleAsync(user, "Doctor");
                        if (roleResult.Succeeded)
                        {
                            var doctorData = new Doctor
                            {
                                Name = register.Name,
                                Email = register.Email,
                                Specialization = register.Specialization // Capture specialization
                            };

                            doctor.Add(doctorData); // Save doctor data including specialization
                            return new CustomResponse<IdentityUser> { StatusCode = 200, Data = user, Message = "Signup Successfully" };
                        }
                        else
                        {
                            var message = string.Join(",", roleResult.Errors.Select(error => error.Description));
                            await userManager.DeleteAsync(user);
                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                        }
                    }
                    else
                    {
                        var message = string.Join(",", result.Errors.Select(error => error.Description));
                        return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                    }
                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                  .Select(e => e.ErrorMessage)
                                                  .ToList();
                    var message = string.Join(",", errors);
                    return new CustomResponse<IdentityUser> { StatusCode = 403, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                var createdUser = await userManager.FindByEmailAsync(register.Email);
                if (createdUser is not null)
                {
                    await userManager.DeleteAsync(createdUser);
                }
                return new CustomResponse<IdentityUser> { StatusCode = 500, Data = null, Message = ex.Message };
            }
        }




        [HttpPost]
        [Route("PharmacistLogin")]

        public async Task<CustomResponse<string>>PharmacistLogin(LoginVM login)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await userManager.FindByEmailAsync(login.Email);
                    if (user is not null)
                    {
                        var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, false, false);
                        if (result.Succeeded)
                        {
                            var roles = await userManager.GetRolesAsync(user);
                            if (roles[0] == "USER" || roles[0] == "ADMIN")
                            {

                                return new CustomResponse<string> { StatusCode = 200, Data = roles[0], Message = "OK" };


                            }
                            else
                            {
                                return new CustomResponse<string> { StatusCode = 401, Data = null, Message = "Not Pharmacist Account" };
                            }
                        }
                       
                        else
                        {

                            return new CustomResponse<string> { StatusCode = 400, Data = null, Message = "Invalid userName or Password Attempt" };

                        }
                    }
                    else
                    {
                        return new CustomResponse<string> { StatusCode = 400, Data = null, Message = "Invalid userName or Password Attempt" };
                    }

                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                        .Select(e => e.ErrorMessage)
                                                        .ToList();
                    var message = string.Join(",", errors);

                    return new CustomResponse<string> { StatusCode = 403, Data = null, Message = message };
                }


            }
            catch (Exception ex)
            {
                return new CustomResponse<string> { StatusCode = 500, Data = null, Message = ex.Message };

            }
        }



        [HttpPost]
        [Route("PharmacistAdminRegister")]
        public async Task<CustomResponse<IdentityUser>> PharmacistAdminRegister(RegisterVM register)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = new IdentityUser
                    {
                        Email = register.Email,
                        UserName = register.Email,

                    };
                    var result = await userManager.CreateAsync(user, register.Password);
                    if (result.Succeeded)
                    {


                        var rolereuslt = await userManager.AddToRoleAsync(user, "ADMIN");
                        if (rolereuslt.Succeeded)
                        {
                            var pharmacistdata = new Pharmacist
                            {
                                Email = register.Email,
                                Name = register.Name
                            };
                            pharmacist.Add(pharmacistdata);
                            return new CustomResponse<IdentityUser> { StatusCode = 200, Data = user, Message = "Signup Successfully" };

                        }
                        else
                        {
                            var message = "";
                            foreach (var item in rolereuslt.Errors)
                            {
                                message += $"{item.Description},";
                            }
                            var createdUser = await userManager.FindByEmailAsync(user.Email);
                            await userManager.DeleteAsync(createdUser);
                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                        }





                    }
                    else
                    {
                        var message = "";
                        foreach (var item in result.Errors)
                        {
                            message += $"{item.Description},";
                        }
                        return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };

                    }
                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                    var message = string.Join(",", errors);

                    return new CustomResponse<IdentityUser> { StatusCode = 403, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                var createdUser = await userManager.FindByEmailAsync(register.Email);
                if (createdUser is not null)
                {
                    await userManager.DeleteAsync(createdUser);
                }

                return new CustomResponse<IdentityUser> { StatusCode = 500, Data = null, Message = ex.Message };

            }
        }


        [HttpPost]
        [Route("PharmacistUserRegister")]
        public async Task<CustomResponse<IdentityUser>> PharmacistUserRegister(RegisterVM register)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = new IdentityUser
                    {
                        Email = register.Email,
                        UserName = register.Email,

                    };
                    var result = await userManager.CreateAsync(user, register.Password);
                    if (result.Succeeded)
                    {


                        var rolereuslt = await userManager.AddToRoleAsync(user,"USER");
                        if (rolereuslt.Succeeded)
                        {
                            var pharmacistdata = new Pharmacist
                            {
                                Email = register.Email,
                                Name = register.Name
                            };
                            pharmacist.Add(pharmacistdata);
                            return new CustomResponse<IdentityUser> { StatusCode = 200, Data = user, Message = "Signup Successfully" };

                        }
                        else
                        {
                            var message = "";
                            foreach (var item in rolereuslt.Errors)
                            {
                                message += $"{item.Description},";
                            }
                            var createdUser = await userManager.FindByEmailAsync(user.Email);
                            await userManager.DeleteAsync(createdUser);
                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                        }





                    }
                    else
                    {
                        var message = "";
                        foreach (var item in result.Errors)
                        {
                            message += $"{item.Description},";
                        }
                        return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };

                    }
                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                    var message = string.Join(",", errors);

                    return new CustomResponse<IdentityUser> { StatusCode = 403, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                var createdUser = await userManager.FindByEmailAsync(register.Email);
                if (createdUser is not null)
                {
                    await userManager.DeleteAsync(createdUser);
                }

                return new CustomResponse<IdentityUser> { StatusCode = 500, Data = null, Message = ex.Message };

            }
        }

        [HttpPost]
        [Route("ForgetPassword")]
        public async Task<CustomResponse<string>> ForgetPassword(ForgetPasswordVM fg)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(fg.Email);

                if (user is not null )
                {

                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    var encodeToken = Encoding.UTF8.GetBytes(token);
                    var newToken = WebEncoders.Base64UrlEncode(encodeToken);

                    var email = $"{fg.url}?token={newToken}&email={fg.Email}";

                    var state = MailSender.sendmail(fg.Email, email);
                    if (state.Result)
                    {
                        return new CustomResponse<string> { StatusCode = 200, Message = "Email sent Successfully", Data = newToken };
                    }
                    else
                    {
                        return new CustomResponse<string> { StatusCode = 500, Message = "Something Wrong", Data = null };
                    }


                }
                else
                {
                    return new CustomResponse<string> { StatusCode = 404, Message = "User Not Found ", Data = null };
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                var message = string.Join(",", errors);
                return new CustomResponse<string> { StatusCode = 400, Data = null, Message = message };

            }
        }
        [HttpPost]
        [Route("ResetPassword")]
        public async Task<CustomResponse<string>> ResetPassword(ResetPasswordVM resetPassword)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(resetPassword.Email);

                if (user != null)
                {
                    var newToken = WebEncoders.Base64UrlDecode(resetPassword.token);
                    var encodeToken = Encoding.UTF8.GetString(newToken);
                    var results = await userManager.ResetPasswordAsync(user, encodeToken, resetPassword.password);

                    if (results.Succeeded)
                    {
                        return new CustomResponse<string> { StatusCode = 200, Data = null, Message = "Password Reseted Successfully" };
                    }
                    else
                    {
                        var message = "";
                        foreach (var item in results.Errors)
                        {
                            message += $"{item.Description},";
                        }

                        return new CustomResponse<string> { StatusCode = 400, Data = null, Message = message };
                    }



                }
                else
                {
                    return new CustomResponse<string> { StatusCode = 404, Data = null, Message = "user Not Found" };

                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                          .Select(e => e.ErrorMessage)
                                          .ToList();
                var message = string.Join(",", errors);
                return new CustomResponse<string> { StatusCode = 400, Data = null, Message = message };
            }
        }



        [HttpPost("RemoveFromRole")]
        public async Task<CustomResponse<IdentityUser>> RemoveFromRole(UserRoleVM user)
        {
            try
            {
                if (ModelState.IsValid)
                {



                    var userdata = await userManager.FindByEmailAsync(user.Email);
                    if (userdata is not null)
                    {
                        var result = await userManager.RemoveFromRoleAsync(userdata, user.Role);
                        if (result.Succeeded)
                        {
                            return new CustomResponse<IdentityUser> { StatusCode = 200, Data = userdata, Message = $"user Removed From {user.Role} Role" };

                        }
                        else
                        {
                            var message = "";
                            foreach (var item in result.Errors)
                            {
                                message += $"{item.Description},";
                            }
                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = userdata, Message = message };

                        }
                    }
                    else
                    {
                        return new CustomResponse<IdentityUser> { StatusCode = 404, Message = "Not Found User" };
                    }
                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                        .Select(e => e.ErrorMessage)
                                                        .ToList();
                    var message = string.Join(",", errors);
                    return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                }
            }
            catch (Exception ex)
            {
                return new CustomResponse<IdentityUser> { StatusCode = 500, Message = ex.Message };

            }
        }

        [HttpPost("AddToRole")]
        public async Task<CustomResponse<IdentityUser>> AddToRole(UserRoleVM user)
        {
            try
            {
                if (ModelState.IsValid)
                {



                    var userdata = await userManager.FindByEmailAsync(user.Email);
                    if (userdata is not null)
                    {
                        var result = await userManager.AddToRoleAsync(userdata, user.Role);
                        if (result.Succeeded)
                        {
                            return new CustomResponse<IdentityUser> { StatusCode = 200, Data = userdata, Message = $"user Assigned To {user.Role} Role" };

                        }
                        else
                        {
                            var message = "";
                            foreach (var item in result.Errors)
                            {
                                message += $"{item.Description},";
                            }
                            return new CustomResponse<IdentityUser> { StatusCode = 400, Data = userdata, Message = message };

                        }
                    }
                    else
                    {
                        return new CustomResponse<IdentityUser> { StatusCode = 404, Message = "Not Found User" };
                    }
                }
                else
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                        .Select(e => e.ErrorMessage)
                                                        .ToList();
                    var message = string.Join(",", errors);
                    return new CustomResponse<IdentityUser> { StatusCode = 400, Data = null, Message = message };
                }
            }catch(Exception ex)
            {
                return new CustomResponse<IdentityUser> { StatusCode = 500, Message = ex.Message };

            }
        }
    }
}
