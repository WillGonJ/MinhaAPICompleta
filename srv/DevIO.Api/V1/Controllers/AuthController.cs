﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DevIO.Api.Controllers;
using DevIO.Api.Extensions;
using DevIO.Api.ViewModels;
using DevIO.Business.Intefaces;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevIO.Api.V1.Controllers
{
    [ApiVersion("1.0")]
    //[ApiVersion("1.0", Deprecated =true)]
    [Route("api/v{version:apiVersion}/")]
    //[DisableCors]
    public class AuthController : MainController
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;

        public AuthController(INotificador notificador,
                                SignInManager<IdentityUser> signInManager,
                                UserManager<IdentityUser> userManager,
                                IOptions<AppSettings> appSettings,
                                IUser user, ILogger<AuthController> logger) : base(notificador, user)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _appSettings = appSettings.Value;
        }
        
        [HttpPost("nova-conta")]
        public async Task<ActionResult> Registrar(RegisterUserViewModel registerUser)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var user = new IdentityUser
            {
                UserName = registerUser.Email,
                Email = registerUser.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, registerUser.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return CustomResponse(await GerarJwt(user.Email));

            }
            foreach (var error in result.Errors)
            {
                NotificarErro(error.Description);
            }

            return CustomResponse(registerUser);
        }

        [HttpPost("entrar")]
        public async Task<ActionResult> Login(LoginUserViewModel loginUser)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await _signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation(message: "Usuario logado " + loginUser.Email + " com sucesso");
                return CustomResponse(await GerarJwt(loginUser.Email));
            }

            if (result.IsLockedOut)
            {
                NotificarErro(mensagem: "Usuário temporariamente bloqueado por tentativas inválidas");
                return CustomResponse(loginUser);
            }

            NotificarErro(mensagem: "Usuário ou Senha incorretos");
            return CustomResponse(loginUser);
           
        }
        private async Task<LoginResponseViewModel> GerarJwt(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            var claims = await _userManager.GetClaimsAsync(user);
            var userRoles = await _userManager.GetRolesAsync(user);

            claims.Add(new Claim(type: JwtRegisteredClaimNames.Sub, value: user.Id));
            claims.Add(new Claim(type: JwtRegisteredClaimNames.Email, value: user.Email));
            claims.Add(new Claim(type: JwtRegisteredClaimNames.Jti, value: Guid.NewGuid().ToString()));
            claims.Add(new Claim(type: JwtRegisteredClaimNames.Nbf, value: ToUnixEpochDate(DateTime.UtcNow).ToString()));
            claims.Add(new Claim(type: JwtRegisteredClaimNames.Iat, value: ToUnixEpochDate(DateTime.UtcNow).ToString(),ClaimValueTypes.Integer64));
            foreach (var userRole in userRoles )
            {
                claims.Add(new Claim(type: "role", value: userRole));
            }

            var identityClaims = new ClaimsIdentity();
            identityClaims.AddClaims(claims);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _appSettings.Emissor,
                Audience = _appSettings.ValidoEm,
                Subject = identityClaims,
                Expires = DateTime.UtcNow.AddHours(_appSettings.ExpiracaoHoras),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), algorithm:SecurityAlgorithms.HmacSha256Signature)
            });

            var encodedToken = tokenHandler.WriteToken(token);
            var response = new LoginResponseViewModel
            {
                AccessToken = encodedToken,
                ExperesIn = TimeSpan.FromHours(_appSettings.ExpiracaoHoras).TotalSeconds,
                UserToken = new UserTokenViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    Claims = claims.Select(c=> new ClaimViewModel { Type = c.Type, Value = c.Value})
                }
            };
            return response;
        }

        private static long ToUnixEpochDate(DateTime date)
            => (long)Math.Round((date.ToUniversalTime() - new DateTimeOffset(year: 1970, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)).TotalSeconds);
    }
}
