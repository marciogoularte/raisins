﻿using Raisins.Data.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EF = Raisins.Data.Models;
using D = Raisins.Accounts.Models;
using System.Data.Entity;
using Raisins.Accounts.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Raisins.Data.Repository
{
    public class AccountRepository : IAccountRepository
    {
        private RaisinsContext _context;
        public AccountRepository() : this(new RaisinsContext())
        {
        }

        public AccountRepository(RaisinsContext context)
        {
            _context = context;
        }
        public D.Account Get(string userName)
        {
            return ConvertToDomain(_context.Accounts
                    .FirstOrDefault(a => a.UserName == userName));
        }

        //public Account GetCurrentUserAccount()
        //{
        //    var http = ObjectProvider.CreateHttpHelper();
        //    var userName = http.GetCurrentUserName();
        //    return GetUserAccount(userName);
        //}
        public D.Accounts GetAll()
        {
            return ConvertToDomainList(_context.Accounts);
        }

        public bool Exists(string userName)
        {
            if (Get(userName) == null) return false;
            else return true;
        }

        public void Edit(D.Account account, D.AccountProfile profile)
        {
            EF.Account efAccount = ConvertToEFwithID(account, profile);

            //START
            var tempAccount = _context.Accounts.Single(a => a.AccountID == efAccount.AccountID);
            tempAccount.RoleID = efAccount.RoleID;

            var tempProfile = _context.Profiles.Single(p => p.ProfileID == efAccount.ProfileID);
            //remove beneficary
            foreach (var ben in tempProfile.Beneficiaries.ToArray())
            {
                if (!efAccount.Profile.Beneficiaries.Any(b => b.BeneficiaryID == ben.BeneficiaryID))
                {
                    tempProfile.Beneficiaries.Remove(ben);
                }
            }
            //add beneficary
            foreach (var ben in efAccount.Profile.Beneficiaries)
            {
                if (!tempProfile.Beneficiaries.Any(b => b.BeneficiaryID == ben.BeneficiaryID))
                {
                    tempProfile.Beneficiaries.Add(ben);
                }
            }           

            _context.SaveChanges();           

        }

        public bool Any(string userName)
        {
            return _context.Accounts.Any(a => a.UserName == userName);
        }

        public void Add(D.Account account, D.AccountProfile profile)
        {
            account.AddSalt();
            _context.Accounts.Add(ConvertToEF(account, profile));
            _context.SaveChanges();
            var x = _context.Accounts.FirstOrDefault(a => a.UserName == account.UserName);
            Debug.WriteLine(x.UserName);
        }

        private EF.Account ConvertToEF(D.Account account, D.AccountProfile profile)
        {
            int roleID = _context.Roles.FirstOrDefault(r => r.Name == account.Role.Name).RoleID;
            List<EF.Beneficiary> efBeneficiaries = new List<EF.Beneficiary>();
            foreach (var beneficiary in profile.Beneficiaries)
            {
                efBeneficiaries.Add(_context.Beneficiaries.FirstOrDefault(b => b.Name == beneficiary.Name));
            }
            EF.AccountProfile efProfile = new EF.AccountProfile(profile.Name, efBeneficiaries);
            return new EF.Account(account.UserName, GetHash(account.Password, account.Salt), account.Salt, roleID, efProfile);
        }

        private EF.Account ConvertToEFwithID(D.Account account, D.AccountProfile profile)
        {
            EF.Role role = _context.Roles.FirstOrDefault(r => r.Name == account.Role.Name);
            List<EF.Beneficiary> efBeneficiaries = new List<EF.Beneficiary>();
            foreach (var beneficiary in profile.Beneficiaries)
            {
                efBeneficiaries.Add(_context.Beneficiaries.FirstOrDefault(b => b.Name == beneficiary.Name));
            }
            EF.Account current = _context.Accounts.FirstOrDefault(a => a.UserName == account.UserName);
            int profileID = _context.Profiles.FirstOrDefault(a => a.Name == profile.Name).ProfileID;

            EF.AccountProfile efProfile = new EF.AccountProfile(profileID, profile.Name, efBeneficiaries, profile.IsLocal);
            return new EF.Account(current.AccountID, account.UserName, current.Password, current.Salt, role.RoleID, role, efProfile.ProfileID, efProfile);
        }

        private EF.AccountProfile ConvertProfileToEF(D.AccountProfile profile)
        {
            List<EF.Beneficiary> efBeneficiaries = new List<EF.Beneficiary>();
            foreach (var beneficiary in profile.Beneficiaries)
            {
                efBeneficiaries.Add(_context.Beneficiaries.FirstOrDefault(b => b.Name == beneficiary.Name));
            }
            int profileID = _context.Profiles.FirstOrDefault(a => a.Name == profile.Name).ProfileID;

            return new EF.AccountProfile(profileID, profile.Name, efBeneficiaries, profile.IsLocal);
        }
        private D.Account ConvertToDomain(EF.Account efAccount)
        {
            EF.Role efRole = _context.Roles.FirstOrDefault(r => r.RoleID == efAccount.RoleID);
            EF.AccountProfile efProfile = _context.Profiles.FirstOrDefault(r => r.ProfileID == efAccount.Profile.ProfileID);
            List<D.Beneficiary> beneficiaries = new List<D.Beneficiary>();
            foreach (var beneficiary in efProfile.Beneficiaries)
            {
                beneficiaries.Add(new D.Beneficiary(beneficiary.Name));
            }

            D.Role role = new D.Role(efRole.Name, efRole.Permissions);
            D.AccountProfile profile = new D.AccountProfile(efProfile.Name, beneficiaries);

            return new D.Account(efAccount.UserName, efAccount.Password, efAccount.Salt, role, profile);
        }

        private D.Accounts ConvertToDomainList(IEnumerable<EF.Account> efAccounts)
        {
            D.Accounts accounts = new D.Accounts();
            foreach (var efAccount in efAccounts)
            {
                accounts.Add(ConvertToDomain(efAccount));
            }
            return accounts;
        }

        private string GetHash(string password, string salt)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
            string result = BitConverter.ToString(bytes).Replace("-", string.Empty);
            return result.ToLower();
        }
    }
}