using Azure.Core;
using Castle.Components.DictionaryAdapter;
using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Dtos;
using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroServiceUnitTests
{
    public static class TestData
    {
        public const string ValidPassword = "Correct-Horse-Battery-Staple-1";

        public static UserEntity User(
            Guid? id = null,
            string? email = "ada@example.com",
            string userName = "ada.lovelace",
            string displayName = "Ada Lovelace",
            DateTime? lastSeen = null)
        {
            return new UserEntity
            {
                Id = id ?? Guid.CreateVersion7(),
                Email = email,
                NormalizedEmail = email?.ToUpperInvariant(),
                UserName = userName,
                NormalizedUserName = userName?.ToUpperInvariant(),
                DisplayName = displayName,
                LastSeen = lastSeen
            };
        }

        public static UserModel UserModel(
            Guid? id = null,
            string email = "ada@example.com",
            string userName = "ada.lovelace",
            string displayName = "Ada Lovelace",
            DateTime? lastSeen = null
            )
        {
            return new UserModel
            {
                Id = id ?? Guid.CreateVersion7(),
                Email = email,
                UserName = userName,
                DisplayName = displayName,
                LastSeen = lastSeen
            };

        }








        public static LoginModel LoginModel(string identfier = "ada@example.com", string password = ValidPassword)
        {
            return new LoginModel
            {
                Identifier = identfier,
                Password = password
            };
        }

        public static LoginDto LoginDto(string identifier = "ada@example.com", string password = ValidPassword)
        {
            return new LoginDto
            {
                Identifier = identifier,
                Password = password
            };
        }

        public static AccessTokenModel AccessToken(string token = "header.payload.signature", DateTime? expiresAtUtc = null)
        {
            return new AccessTokenModel
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(120)
            };
        }

        public static LoginResultModel LoginResult(UserModel? user = null, AccessTokenModel? token = null)
        {
            return new LoginResultModel
            {
                AccessToken = token ?? AccessToken(),
                User = user ?? UserModel()
            };
        }

    }
}
