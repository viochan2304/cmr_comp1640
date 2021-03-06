﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;
using CMR.Custom;
using CMR.Models;
using CMR.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;

namespace CMR.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _msgs = new List<string>();
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>(); }
            private set { _signInManager = value; }
        }

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>(); }
            private set { _userManager = value; }
        }

        [AccessDeniedAuthorize(Roles = "Administrator")]
        public ActionResult Index(string username, string role)
        {
            var aivm = new AccountIndexViewModel();
            aivm.FilterUsername = username;
            aivm.FilterRole = role;

            var urvms = new List<UserRoleViewModel>();
            var users = new List<ApplicationUser>();
            if (!username.IsEmpty() && !role.IsEmpty())
            {
                users =
                    UserManager.Users.Where(u => u.Roles.Any(r => r.RoleId == role))
                        .Where(u => u.UserName.Contains(username))
                        .ToList();
            }
            else if (!username.IsEmpty() && role.IsEmpty())
            {
                users = UserManager.Users.Where(u => u.UserName.Contains(username)).ToList();
            }
            else if (username.IsEmpty() && !role.IsEmpty())
            {
                users = UserManager.Users.Where(u => u.Roles.Any(r => r.RoleId == role)).ToList();
            }
            else
                users = UserManager.Users.ToList();

            foreach (var user in users)
            {
                var urvm = new UserRoleViewModel();
                urvm.User = user;
                urvm.Roles = UserManager.GetRoles(user.Id).ToList();
                urvms.Add(urvm);
            }
            aivm.Urvms = urvms;

            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(new ApplicationDbContext()));
            aivm.Roles = roleManager.Roles.ToList();
            return View(aivm);
        }

        [AllowAnonymous]
        public ActionResult Denied()
        {
            return View();
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, change to shouldLockout: true
            var result =
                await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AccessDeniedAuthorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    Fullname = model.Fullname,
                    Email = model.Email
                };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var addRoleresult = await UserManager.AddToRoleAsync(user.Id, "Guest");
                    if (addRoleresult.Succeeded)
                    {
                        await UserManager.AddClaimAsync(user.Id, new Claim("Fullname", user.Fullname));
                        _msgs.Add("New Account Created.");
                        TempData["msgs"] = _msgs;
                        return RedirectToAction("Index", "Account");
                    }
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ResetPassword
        [AccessDeniedAuthorize(Roles = "Administrator")]
        public ActionResult ResetPassword(string id)
        {
            if (id == null)
                return HttpNotFound();

            var user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AccessDeniedAuthorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(string id, ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return HttpNotFound();
            }
            var resultValidatePassword = await UserManager.PasswordValidator.ValidateAsync(model.Password);
            if (!resultValidatePassword.Succeeded)
            {
                AddErrors(resultValidatePassword);
                return View(model);
            }
            var newHashPassword = UserManager.PasswordHasher.HashPassword(model.Password);
            user.PasswordHash = newHashPassword;
            var result = await UserManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _msgs.Add("Success reset password for: " + user.UserName);
                TempData["msgs"] = _msgs;
                return RedirectToAction("Index", "Account");
            }
            AddErrors(result);
            return View(model);
        }


        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        [AccessDeniedAuthorize(Roles = "Administrator")]
        public ActionResult ChangeRole(string id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            var crvm = new ChangeRoleViewModel();
            crvm.User = user;
            crvm.Roles = new ApplicationDbContext().Roles.ToList();
            return View(crvm);
        }

        [HttpPost]
        [AccessDeniedAuthorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeRole(string id, string role)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var cRole = await UserManager.GetRolesAsync(user.Id);
            if (cRole != null && cRole.Any())
            {
                var removeRoleResult = UserManager.RemoveFromRoleAsync(user.Id, cRole.FirstOrDefault());
                if (!removeRoleResult.Result.Succeeded)
                {
                    _errors.Add("Failed to remove old role");
                    TempData["errors"] = _errors;
                    return View();
                }
            }

            var nRole = new ApplicationDbContext().Roles.Find(role);
            if (nRole != null)
            {
                var addRoleResult = await UserManager.AddToRoleAsync(user.Id, nRole.Name);
                if (addRoleResult.Succeeded)
                {
                    _msgs.Add("Success change role for user: " + user.UserName);
                    TempData["msgs"] = _msgs;
                    return RedirectToAction("Index", "Account");
                }
            }
            else
            {
                _errors.Add("Invalid role");
            }

            TempData["errors"] = _errors;
            return View();
        }

        [AccessDeniedAuthorize(Roles = "Administrator")]
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            EditViewModel evm = new EditViewModel();
            evm.Fullname = user.Fullname;
            evm.Email = user.Email;
            return View(evm);
        }

        [AccessDeniedAuthorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, EditViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByIdAsync(id);
                if (user == null)
                {
                    return HttpNotFound();
                }
                user.Fullname = model.Fullname;
                user.Email = model.Email;
                var result = UserManager.UpdateAsync(user);
                if (result.Result.Succeeded)
                {
                    _msgs.Add("User " + user.UserName + " updated.");
                    TempData["msgs"] = _msgs;
                    return RedirectToAction("Index", "Account");
                }
            }
            var errors = ModelState.Where(ms => ms.Value.Errors.Count > 0).ToList();
            foreach (var error in errors)
            {
                _errors.Add(error.Value.ToString());
            }
            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers

        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get { return HttpContext.GetOwinContext().Authentication; }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties {RedirectUri = RedirectUri};
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        #endregion
    }
}