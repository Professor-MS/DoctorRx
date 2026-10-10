using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class DoctorSetupDrawerTests
{
    [Fact]
    public async Task CreateDoctorAsync_WhenDoctorAlreadyExists_ReturnsFailureResultWithoutThrowing()
    {
        var stub = new InMemoryDoctorService();
        await stub.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Dr. John Doe",
            Qualification = "MBBS",
            RegistrationNumber = "REG-111",
            Specialization = "General Physician",
            ClinicName = "Main Clinic"
        });

        // Act: Attempt to create a second doctor
        var result = await stub.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Dr. Jane Smith",
            Qualification = "MD",
            RegistrationNumber = "REG-222",
            Specialization = "Cardiologist",
            ClinicName = "Heart Clinic"
        });

        // Assert: Must return failure Result, never throw
        Assert.False(result.IsSuccess);
        Assert.Equal("An active doctor profile already exists.", result.ErrorMessage);
        Assert.Single(stub.Doctors);
    }

    [Fact]
    public async Task SaveDoctorSetup_WhenActiveDoctorExists_UsesUpdatePath_NeverThrows_AndNeverInsertsSecondRow()
    {
        var docService = new InMemoryDoctorService();
        var initial = await docService.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Dr. Original",
            Qualification = "MBBS",
            RegistrationNumber = "REG-12345",
            Specialization = "Physician",
            ClinicName = "Clinic Alpha"
        });
        Assert.True(initial.IsSuccess);
        Assert.Single(docService.Doctors);

        var dialogService = new TestDialogService();
        var mainVm = new MainWindowViewModel(
            new TestNavigationService(),
            docService,
            dialogService,
            new TestDraftService(),
            new TestWindowPlacementService());

        // Open setup drawer for editing
        await mainVm.InitializeAsync();
        mainVm.OpenDoctorSetupCommand.Execute(null);

        // Update details in drawer
        mainVm.DoctorSetupName = "Dr. Updated Name";
        mainVm.DoctorSetupQualification = "MBBS, MD";
        mainVm.DoctorSetupSpecialization = "Consultant Physician";
        mainVm.DoctorSetupClinicName = "Clinic Beta";
        mainVm.DoctorSetupRegistrationNumber = "REG-99999";

        int createsBefore = docService.CreateCallCount;

        // Act: Save drawer
        var exception = await Record.ExceptionAsync(async () =>
        {
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)mainVm.SaveDoctorSetupCommand).ExecuteAsync(null);
        });

        // Assert: Never throws
        Assert.Null(exception);

        // Drawer must close
        Assert.False(mainVm.IsDoctorSetupOpen);

        // Must update existing row, never insert a second row
        Assert.Single(docService.Doctors);
        Assert.Equal(1, docService.UpdateCallCount);
        Assert.Equal(createsBefore, docService.CreateCallCount); // No new creates during SaveDoctorSetup
        Assert.Equal("Dr. Updated Name", docService.Doctors[0].Name);
        Assert.Equal("Clinic Beta", docService.Doctors[0].ClinicName);
    }

    private class InMemoryDoctorService : IDoctorService
    {
        public List<DoctorDto> Doctors { get; } = new();
        public int CreateCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Doctors.FirstOrDefault());
        }

        public Task<Result<DoctorDto>> CreateDoctorAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            if (Doctors.Any())
            {
                return Task.FromResult(Result<DoctorDto>.Failure("An active doctor profile already exists."));
            }

            var doc = new DoctorDto(
                1,
                dto.Name,
                dto.Qualification,
                dto.RegistrationNumber,
                dto.Specialization,
                dto.Phone,
                dto.Email,
                dto.ClinicName,
                dto.ClinicAddress,
                dto.ClinicPhone,
                dto.HeaderText,
                dto.FooterText,
                dto.TitlePrefix ?? "Dr.",
                dto.RegistrationLabel ?? "Reg. No.");

            Doctors.Add(doc);
            return Task.FromResult(Result<DoctorDto>.Success(doc));
        }

        public Task<Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            var index = Doctors.FindIndex(d => d.Id == dto.Id);
            if (index < 0 && Doctors.Count > 0)
            {
                index = 0; // fallback to active
            }

            if (index < 0)
            {
                return Task.FromResult(Result<DoctorDto>.Failure("Doctor not found."));
            }

            var doc = new DoctorDto(
                dto.Id,
                dto.Name,
                dto.Qualification,
                dto.RegistrationNumber,
                dto.Specialization,
                dto.Phone,
                dto.Email,
                dto.ClinicName,
                dto.ClinicAddress,
                dto.ClinicPhone,
                dto.HeaderText,
                dto.FooterText,
                dto.TitlePrefix,
                dto.RegistrationLabel);

            Doctors[index] = doc;
            return Task.FromResult(Result<DoctorDto>.Success(doc));
        }

        public Task<IReadOnlyList<DoctorDto>> GetAllDoctorsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoctorDto>>(Doctors.ToList());
        }

        public Task<Result<DoctorDto>> SwitchActiveDoctorAsync(int id, CancellationToken cancellationToken = default)
        {
            var doc = Doctors.FirstOrDefault(d => d.Id == id);
            return Task.FromResult(doc != null ? Result<DoctorDto>.Success(doc) : Result<DoctorDto>.Failure("Doctor not found."));
        }
    }

    private class TestNavigationService : INavigationService
    {
        public NavigationDestination CurrentDestination => NavigationDestination.Dashboard;
        public NavigationDestination? PreviousDestination => null;
        public ViewModelBase? CurrentViewModel => null;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }
        public void NavigateTo(NavigationDestination destination, object? parameter = null) { }
        public void GoBack() { }
    }

    private class TestDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class TestDraftService : IDraftService
    {
        public Task<Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default) => Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        public Task<Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(Result<PrescriptionComposerState>.Failure("None"));
        public Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DraftSummaryDto>>(new List<DraftSummaryDto>());
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public void MarkFinalized(Guid draftKey) { }
        public bool IsFinalized(Guid draftKey) => false;
    }

    private class TestWindowPlacementService : IWindowPlacementService
    {
        public WindowPlacementSettings? LoadPlacement() => null;
        public void SavePlacement(WindowPlacementSettings settings) { }
        public void ApplyPlacement(System.Windows.Window window) { }
        public void PersistPlacement(System.Windows.Window window, bool? isSidebarCollapsedOverride = null) { }
        public bool IsOnScreen(double left, double top, double width, double height) => true;
    }
}
