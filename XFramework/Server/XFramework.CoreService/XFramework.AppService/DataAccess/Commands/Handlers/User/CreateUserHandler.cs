using MediatR;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.BO;
using XFramework.Domain.DTO;
using XFramework.Domain.Enums;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
   public class CreateUserHandler : CommandBaseHandler, IRequestHandler<CreateUserCmd, bool>
    {
        public CreateUserHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }

        public async Task<bool> Handle(CreateUserCmd request, CancellationToken cancellationToken)
        {
            using (var transaction = _dataLayer.Database.BeginTransaction())
            {
                // Store User Info Data
                TblUserInfo userInfo = new TblUserInfo
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Dob = (DateTime)request.Dob,
                    CivilStatus = request.CivilStatus,
                    Gender = (short)request.Gender,
                    IsVerified = false,
                    Uid = Guid.NewGuid().ToString()
                };

                _dataLayer.Add(userInfo);
                await _dataLayer.SaveChangesAsync();

                // Create hash for the user password as bytea
                byte[] _passwordByte = Encoding.ASCII.GetBytes(request.PasswordString);
                byte[] _hashPasswordByte;
                SHA512 shaM = new SHA512Managed();
                _hashPasswordByte = shaM.ComputeHash(_passwordByte);

                // Store User Credentials data
                TblUserCredentials userCredentials = new TblUserCredentials()
                {
                    UserInfoId = userInfo.Id,
                    UserName = request.UserName,
                    LogInStatus = (short?)LoginStatus.Enabled,
                    PasswordByte = _hashPasswordByte
                };

                _dataLayer.Add(userCredentials);
                await _dataLayer.SaveChangesAsync();

                // Setup User Role Data
                // Initialize ApplicationCommandHandler

                TblUserRoles userRoles = new TblUserRoles()
                {
                    UserCredId = userCredentials.Id,
                    RoleExpiration = request.Expiration.Date != DateTime.Parse("1/1/0001") ? request.Expiration : (DateTime)request.Application.Expiration,
                    RoleEntityId = (long?)(request.Role == UserRole.None ? UserRole.Default : request.Role)
                };

                _dataLayer.Add(userRoles);
                await _dataLayer.SaveChangesAsync();

                transaction.Commit();
            }

            return true;
        }
    }
}
