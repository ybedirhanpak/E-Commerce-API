﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

//Imports
using e_commerce_api.Helpers;
using e_commerce_api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

//MongoDB
using MongoDB.Driver;

namespace e_commerce_api.Services
{
    public interface IUserService
    {
        User Authenticate(string email, string password);
        IEnumerable<User> GetAll();
        User GetById(string id);
        User GetByEmail(string email);
        User Create(User user, string password);
        User Update(User user, string password = null);
        void Delete(string id);
        string GenerateToken(User user, AppSettings _appSettings);
    }

    public class UserService : IUserService
    {
        //Create a collection object to refer users
        private readonly IMongoCollection<User> _users;

        public UserService(IECommerceDatabaseSettings databaseSettings)
        {
            //Create a mongo client instance to connect mongo server
            var client = new MongoClient(databaseSettings.ConnectionString);
            //Create the database which is "e-commerce"
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            //Initialize collection by getting from mongodb
            _users = database.GetCollection<User>(databaseSettings.UsersCollectionName);

        }

        public User Authenticate(string email, string password)
        {
            if(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            var user = _users.Find<User>(x => x.Email == email ).FirstOrDefault();

            // return null if user not found
            if (user == null)
                return null;

            if(!VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt)) {
                return null;
            }

            return user;
        }

        public IEnumerable<User> GetAll()
        {
            return _users.Find(u => true).ToList();
        }

        public User GetById(string id)
        {
            return _users.Find<User>(u => u.Id == id).FirstOrDefault();
        }

        public User GetByEmail (string email)
        {
            return _users.Find<User>(u => u.Email == email).FirstOrDefault();
        }

        public User Create(User user, string password)
        {
            //validation
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new AppException("User not found");
            }

            if (_users.Find<User>(u => u.Email == user.Email).Any<User>())
            {
                throw new AppException("Email " + user.Email + " already exists.");
                
            }

            byte[] passwordHash, passwordSalt;
            CreatePasswordHash(password, out passwordHash, out passwordSalt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;

            _users.InsertOne(user);
            return user;

        }

        public User Update(User userIn, string password=null)
        {
            var user = _users.Find<User>(u => u.Id == userIn.Id).FirstOrDefault();

            if (user == null) throw new AppException("User not found");

            if(userIn.Email != null && userIn.Email != user.Email)
            {
                if(_users.Find<User>(u => u.Email == userIn.Email).Any())
                {
                    throw new AppException("Email " + userIn.Email + " is already exists.");
                }
            }

            //update user properties
            if (!string.IsNullOrWhiteSpace(userIn?.FirstName))
            {
                user.FirstName = userIn.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(userIn?.LastName))
            {
                user.LastName = userIn.LastName;
            }

            if (!string.IsNullOrWhiteSpace(userIn?.Role))
            {
                user.Role = userIn.Role;
            }

            if (userIn?.Addresses?.Count() > 0)
            {
                user.Addresses = userIn.Addresses;
            }

            if (userIn?.Orders?.Count() > 0)
            {
                //TODO CHANGE HERE
                var MergedOrders = new string[user.Orders.Length + userIn.Orders.Length];

                user.Orders.CopyTo(MergedOrders, 0);
                userIn.Orders.CopyTo(MergedOrders, user.Orders.Length);

                user.Orders = MergedOrders;
            }

            //update password if it was provided
            if (!string.IsNullOrWhiteSpace(password))
            {
                byte[] passwordHash, passwordSalt;
                CreatePasswordHash(password, out passwordHash, out passwordSalt);

                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;

            }

            _users.ReplaceOne<User>(u => u.Id == user.Id, user);
            return user;

        }

        public void Delete(string id)
        {
            var user = _users.Find<User>(u => u.Id == id).FirstOrDefault();

            if(user!=null)
            {
                _users.DeleteOne(u => u.Id == id);
            }
        }

        public string GenerateToken(User user, AppSettings _appSettings)
        {
            // authentication successful so generate jwt token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return tokenString;

        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt )
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
                
            }
            if (string.IsNullOrWhiteSpace(password))
            {

                throw new ArgumentException("Value cannot be empty or whitespace only string.",nameof(password));
            }

            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Value cannot be empty....",nameof(password));
            if (storedHash.Length != 64) throw new ArgumentException("Invalid length of password hash(64 bytes expected)", nameof(storedHash));
            if (storedSalt.Length != 128) throw new ArgumentException("Invalid length of password salt(128 bytes expected)", nameof(storedSalt));

            using (var hmac = new System.Security.Cryptography.HMACSHA512(storedSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                for (int i = 0; i< computedHash.Length; i++)
                {
                    if (computedHash[i] != storedHash[i]) return false;
                }
            }

            return true;
        }
    }
}
