using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaveHere.Services;
using SaveHere.Models;
using SaveHere.Models.db;

namespace SaveHere.Tests.Services
{
    public class UserManagementServiceTests : IDisposable
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<ILogger<UserManagementService>> _loggerMock;
        private readonly UserManagementService _service;
        private readonly AppDbContext _context;

        public UserManagementServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new AppDbContext(options);
            
            // Mock UserManager
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
            
            _loggerMock = new Mock<ILogger<UserManagementService>>();

            _service = new UserManagementService(
                _userManagerMock.Object,
                _context,
                _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task IsRegistrationEnabledAsync_WithEnabledSettings_ReturnsTrue()
        {
            // Arrange
            var settings = new RegistrationSettings { Id = 1, IsRegistrationEnabled = true };
            await _context.RegistrationSettings.AddAsync(settings);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsRegistrationEnabledAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsRegistrationEnabledAsync_WithDisabledSettings_ReturnsFalse()
        {
            // Arrange
            var settings = new RegistrationSettings { Id = 1, IsRegistrationEnabled = false };
            await _context.RegistrationSettings.AddAsync(settings);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsRegistrationEnabledAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsRegistrationEnabledAsync_WithNoSettings_ReturnsFalse()
        {
            // Act
            var result = await _service.IsRegistrationEnabledAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetPendingUsersAsync_ReturnsOnlyDisabledUsers()
        {
            // Arrange
            var users = new List<ApplicationUser>
            {
                new() { Id = "1", UserName = "user1", IsEnabled = false },
                new() { Id = "2", UserName = "user2", IsEnabled = true },
                new() { Id = "3", UserName = "user3", IsEnabled = false }
            };

            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPendingUsersAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, user => Assert.False(user.IsEnabled));
            Assert.Contains(result, user => user.Id == "1");
            Assert.Contains(result, user => user.Id == "3");
        }

        [Fact]
        public async Task GetAllUsersAsync_ReturnsAllUsers()
        {
            // Arrange
            var users = new List<ApplicationUser>
            {
                new() { Id = "1", UserName = "user1", IsEnabled = false },
                new() { Id = "2", UserName = "user2", IsEnabled = true },
                new() { Id = "3", UserName = "user3", IsEnabled = false }
            };

            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllUsersAsync();

            // Assert
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task EnableUserAsync_WithValidUser_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "testuser", IsEnabled = false };
            
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);
            _userManagerMock.Setup(x => x.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.EnableUserAsync("1");

            // Assert
            Assert.True(result);
            Assert.True(user.IsEnabled);
            _userManagerMock.Verify(x => x.FindByIdAsync("1"), Times.Once);
            _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task EnableUserAsync_WithInvalidUser_ReturnsFalse()
        {
            // Arrange
            _userManagerMock.Setup(x => x.FindByIdAsync("invalid"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _service.EnableUserAsync("invalid");

            // Assert
            Assert.False(result);
            _userManagerMock.Verify(x => x.FindByIdAsync("invalid"), Times.Once);
            _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task EnableUserAsync_WithUpdateFailure_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "testuser", IsEnabled = false };
            var errors = new[] { new IdentityError { Code = "Error", Description = "Update failed" } };
            
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);
            _userManagerMock.Setup(x => x.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(errors));

            // Act
            var result = await _service.EnableUserAsync("1");

            // Assert
            Assert.False(result);
            _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task DisableUserAsync_WithNonAdminUser_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "testuser", IsEnabled = true };
            
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);
            _userManagerMock.Setup(x => x.IsInRoleAsync(user, "Admin"))
                .ReturnsAsync(false);
            _userManagerMock.Setup(x => x.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.DisableUserAsync("1");

            // Assert
            Assert.True(result);
            Assert.False(user.IsEnabled);
            _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task DisableUserAsync_WithLastAdminUser_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "admin", IsEnabled = true };
            var adminUsers = new List<ApplicationUser> { user };
            
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);
            _userManagerMock.Setup(x => x.IsInRoleAsync(user, "Admin"))
                .ReturnsAsync(true);
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(adminUsers);

            // Act
            var result = await _service.DisableUserAsync("1");

            // Assert
            Assert.False(result);
            Assert.True(user.IsEnabled); // Should remain enabled
            _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task DisableUserAsync_WithMultipleAdmins_ReturnsTrue()
        {
            // Arrange
            var user1 = new ApplicationUser { Id = "1", UserName = "admin1", IsEnabled = true };
            var user2 = new ApplicationUser { Id = "2", UserName = "admin2", IsEnabled = true };
            var adminUsers = new List<ApplicationUser> { user1, user2 };
            
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user1);
            _userManagerMock.Setup(x => x.IsInRoleAsync(user1, "Admin"))
                .ReturnsAsync(true);
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(adminUsers);
            _userManagerMock.Setup(x => x.UpdateAsync(user1))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.DisableUserAsync("1");

            // Assert
            Assert.True(result);
            Assert.False(user1.IsEnabled);
            _userManagerMock.Verify(x => x.UpdateAsync(user1), Times.Once);
        }

        [Fact]
        public async Task SetRegistrationEnabledAsync_WithNoExistingSettings_CreatesNewSettings()
        {
            // Act
            var result = await _service.SetRegistrationEnabledAsync(true);

            // Assert
            Assert.True(result);
            var settings = await _context.RegistrationSettings.FirstOrDefaultAsync();
            Assert.NotNull(settings);
            Assert.True(settings.IsRegistrationEnabled);
        }

        [Fact]
        public async Task SetRegistrationEnabledAsync_WithExistingSettings_UpdatesSettings()
        {
            // Arrange
            var settings = new RegistrationSettings { Id = 1, IsRegistrationEnabled = false };
            await _context.RegistrationSettings.AddAsync(settings);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SetRegistrationEnabledAsync(true);

            // Assert
            Assert.True(result);
            var updatedSettings = await _context.RegistrationSettings.FindAsync(1);
            Assert.NotNull(updatedSettings);
            Assert.True(updatedSettings.IsRegistrationEnabled);
        }

        [Fact]
        public async Task IsUserEnabledAsync_WithEnabledUser_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "testuser", IsEnabled = true };
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);

            // Act
            var result = await _service.IsUserEnabledAsync("1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsUserEnabledAsync_WithDisabledUser_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser { Id = "1", UserName = "testuser", IsEnabled = false };
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(user);

            // Act
            var result = await _service.IsUserEnabledAsync("1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsUserEnabledAsync_WithNonExistentUser_ReturnsFalse()
        {
            // Arrange
            _userManagerMock.Setup(x => x.FindByIdAsync("invalid"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _service.IsUserEnabledAsync("invalid");

            // Assert
            Assert.False(result);
        }
    }
}