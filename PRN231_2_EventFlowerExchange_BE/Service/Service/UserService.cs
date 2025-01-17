﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using BusinessObject;
using BusinessObject.Dto.Response;
using BusinessObject.DTO.Request;
using BusinessObject.DTO.Response;
using BusinessObject.Enum;
using Repository.IRepository;
using Service.IService;

namespace Service.Service
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IMapper _mapper;

        public UserService(IUserRepository userRepository, ICompanyRepository companyRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _companyRepository = companyRepository;
            _mapper = mapper;
        }

        // ========== User CRUD Operations ==========

        public async Task<List<User>> GetAllUsers()
        {
            var users = await _userRepository.GetAllUsers();
            return _mapper.Map<List<User>>(users);
        }

        public async Task<ListUserDTO> GetUserById(int id)
        {
            var user = await _userRepository.GetUserById(id);
            return _mapper.Map<ListUserDTO>(user);
        }

        public async Task<UserResponseDto> CreateUser(CreateUserDTO createUserDTO)
        {
            try
            {
                ValidateInputs(createUserDTO);

                if (await CheckEmailExist(createUserDTO.Email))
                    throw new Exception("Email exists.");
                if (await CheckPhoneExist(createUserDTO.Phone))
                    throw new Exception("Phone exists.");

                EnumList.UserRole userRole = GetUserRole(createUserDTO.Role);

                var user = new User
                {
                    FullName = createUserDTO.FullName,
                    Email = createUserDTO.Email,
                    Phone = createUserDTO.Phone,
                    Address = createUserDTO.Address,
                    Role = userRole,
                    Password = createUserDTO.Password,
                    Status = EnumList.Status.Active
                };

                if (!await _userRepository.AddAsync(user))
                    throw new Exception("Unable to create user.");

                return _mapper.Map<UserResponseDto>(user);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<UserResponseDto> CreateSeller(CreateSellerDTO createSellerDTO, CreateUserSellerDTO createUserSellerDTO)
        {
            try
            {
                ValidateInputs(createUserSellerDTO, createSellerDTO);

                if (await CheckEmailExist(createUserSellerDTO.Email))
                    throw new Exception("Email already exists.");
                if (await CheckPhoneExist(createUserSellerDTO.Phone))
                    throw new Exception("Phone number already exists.");
                if (await CheckCompanyNameExist(createSellerDTO.CompanyName))
                    throw new Exception("Company name already exists.");

                var user = new User
                {
                    FullName = createUserSellerDTO.FullName,
                    Email = createUserSellerDTO.Email,
                    Phone = createUserSellerDTO.Phone,
                    Address = createUserSellerDTO.Address,
                    Role = EnumList.UserRole.Seller,
                    Password = createUserSellerDTO.Password,
                    Status = EnumList.Status.Active
                };

                if (!await _userRepository.AddAsync(user))
                    throw new Exception("Failed to create user.");

                var company = new Company
                {
                    CompanyName = createSellerDTO.CompanyName,
                    CompanyAddress = createSellerDTO.CompanyAddress,
                    CompanyDescription = createSellerDTO.CompanyDescription,
                    TaxNumber = createSellerDTO.TaxNumber,
                    City = createSellerDTO.City,
                    PostalCode = createSellerDTO.PostalCode,
                    UserId = user.UserId,
                    Status = EnumList.Status.Active
                };

                if (!await _companyRepository.AddNew(company))
                    throw new Exception("Failed to create company.");

                return _mapper.Map<UserResponseDto>(user);
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred: {ex.Message}");
            }
        }

        public async Task<bool> UpdateUser(int id, UpdateUserDTO updateUserDTO)
        {
            if (updateUserDTO == null) throw new ArgumentNullException(nameof(updateUserDTO));

            ValidateUpdateInputs(updateUserDTO);

            var existingUser = await _userRepository.GetUserById(id);
            if (existingUser == null) throw new ArgumentException("User does not exist.");

            // Check if the email has changed and if the new email already exists
            if (existingUser.Email != updateUserDTO.Email && await CheckEmailExist(updateUserDTO.Email))
                throw new Exception("The email already exists. Please use a different email.");

            // Check if the phone has changed and if the new phone already exists
            if (existingUser.Phone != updateUserDTO.Phone && await CheckPhoneExist(updateUserDTO.Phone))
                throw new Exception("The phone number already exists. Please use a different phone number.");

            existingUser.FullName = updateUserDTO.FullName;
            existingUser.Email = updateUserDTO.Email;
            existingUser.Phone = updateUserDTO.Phone;
            existingUser.Address = updateUserDTO.Address;
            existingUser.Role = updateUserDTO.Role;
            existingUser.Status = EnumList.Status.Active;

            return await _userRepository.UpdateAsync(existingUser);
        }


        public async Task<bool> DeleteUser(int id)
        {
            return await _userRepository.DeleteAsync(id);
        }



        //VALIDATE PART
        private EnumList.UserRole GetUserRole(string role)
        {
            return role.Equals("Buyer", StringComparison.OrdinalIgnoreCase)
                ? EnumList.UserRole.Buyer
                : role.Equals("Personal", StringComparison.OrdinalIgnoreCase)
                    ? EnumList.UserRole.DeliveryPersonnel
                    : throw new Exception("Invalid role. Only 'Buyer' or 'Personal' roles are allowed.");
        }

        private async Task<bool> CheckEmailExist(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null;
        }

        private async Task<bool> CheckPhoneExist(string phone)
        {
            var users = await _userRepository.GetAllUsers();
            return users.Any(u => u.Phone == phone);
        }

        private async Task<bool> CheckCompanyNameExist(string companyName)
        {
            var companies = await _companyRepository.GetCompanies();
            return companies.Any(c => c.CompanyName == companyName);
        }

        private void ValidateInputs(CreateUserDTO createUserDTO)
        {
            ValidateCommonInputs(createUserDTO.Phone, createUserDTO.Email, createUserDTO.FullName, createUserDTO.Password);
        }

        private void ValidateInputs(CreateUserSellerDTO createUserSellerDTO, CreateSellerDTO createSellerDTO = null)
        {
            ValidateCommonInputs(createUserSellerDTO.Phone, createUserSellerDTO.Email, createUserSellerDTO.FullName, createUserSellerDTO.Password);

            if (createSellerDTO != null)
            {
                ValidateTaxNumber(createSellerDTO.TaxNumber);
                ValidatePostalCode(createSellerDTO.PostalCode);
            }
        }

        private void ValidateUpdateInputs(UpdateUserDTO updateUserDTO)
        {
            ValidatePhoneNumber(updateUserDTO.Phone);
            ValidateEmail(updateUserDTO.Email);
            ValidateFullName(updateUserDTO.FullName);
        }

        private void ValidateCommonInputs(string phone, string email, string fullName, string password)
        {
            ValidatePhoneNumber(phone);
            ValidateEmail(email);
            ValidateFullName(fullName);
            ValidatePassword(password);
        }

        private void ValidatePhoneNumber(string phone)
        {
            if (!Regex.IsMatch(phone, @"^0\d{9}$"))
                throw new ArgumentException("Phone number must start with 0 and contain exactly 10 digits.");
        }

        private void ValidateEmail(string email)
        {
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Invalid email format.");
        }

        private void ValidateFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
                throw new Exception("Full name cannot be empty and must be under 100 characters.");
        }

        private void ValidatePassword(string password)
        {
            if (password.Length < 8 || !Regex.IsMatch(password, @"[A-Z]") || !Regex.IsMatch(password, @"[a-z]") ||
                !Regex.IsMatch(password, @"\d") || !Regex.IsMatch(password, @"[\W_]"))
                throw new Exception("Password must be at least 8 characters long and include uppercase, lowercase, numeric, and special characters.");
        }

        private void ValidateTaxNumber(string taxNumber)
        {
            if (!Regex.IsMatch(taxNumber, @"^\d{10}$"))
                throw new Exception("Tax number must contain exactly 10 digits.");
        }

        private void ValidatePostalCode(string postalCode)
        {
            if (!Regex.IsMatch(postalCode, @"^\d{5,6}$"))
                throw new Exception("Postal code must contain 5 or 6 digits.");
        }
    }
}
