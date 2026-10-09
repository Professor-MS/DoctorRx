using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Presentation.Views;
using Xunit;

namespace DoctorRx.Tests;

public class PrintRenderTests
{
    private PrescriptionDetailDto CreateStubPrescription(bool includeAdvice = true, bool includeComplaints = true)
    {
        var doctorSnapshot = new DoctorSnapshot(
            name: "Dr. Asim Farooq",
            qualification: "MBBS, MD",
            registrationNumber: "PMDC-98765",
            specialization: "Cardiology",
            phone: "03009876543",
            clinicName: "City Cardiology Center",
            clinicAddress: "123 Health Boulevard, Lahore",
            clinicPhone: "042-35889900",
            headerText: "Leading Cardiac Care",
            footerText: "Emergency line open 24/7",
            titlePrefix: "Dr.",
            registrationLabel: "PMDC No."
        );

        var patientSnapshot = new PatientSnapshot(
            name: "Kamran Akmal",
            gender: Gender.Male,
            ageText: "38 yrs",
            phone: "03214567890",
            knownAllergies: "None"
        );

        var items = new List<PrescriptionMedicineDto>
        {
            new(
                Id: 1,
                MedicineId: 10,
                MedicineName: "Atorvastatin",
                GenericName: "Atorvastatin Calcium",
                Form: "Tablet",
                Strength: "20mg",
                Dose: "1 tab",
                Frequency: "Once daily",
                Timing: "At night",
                MealRelation: MealRelation.AfterMeal,
                CustomMealRelationText: null,
                WithWhat: "Water",
                Route: "Oral",
                Duration: "30 days",
                Instructions: "After dinner",
                SortOrder: 1
            )
        };

        return new PrescriptionDetailDto(
            Id: 101,
            PrescriptionNumber: "RX-20261009-0010",
            PatientId: 5,
            PatientSnapshot: patientSnapshot,
            DoctorId: 2,
            DoctorSnapshot: doctorSnapshot,
            PrescriptionDate: DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints: includeComplaints ? "Chest discomfort upon exertion" : null,
            BloodPressure: "130/85",
            PulseRate: "76",
            Temperature: "98.6 F",
            WeightKg: "75",
            ClinicalNotes: "ECG normal sinus rhythm",
            GeneralAdvice: includeAdvice ? "Low sodium diet, brisk walk 30 mins daily" : null,
            FollowUpDate: DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            FollowUpText: includeAdvice ? "Follow up after 2 weeks with lipid profile" : null,
            Status: PrescriptionStatus.Finalized,
            FinalizedAtUtc: DateTime.UtcNow.AddHours(-1),
            CancelledAtUtc: null,
            CancellationReason: null,
            ParentPrescriptionId: null,
            AmendmentNumber: 0,
            Version: 1,
            Items: items
        );
    }

    [Fact]
    public void PrescriptionPaperView_Render_OmittedEmptySections_AndNoShadowPixels()
    {
        StaTestRunner.Run(() =>
        {
            var rx = CreateStubPrescription(includeAdvice: false, includeComplaints: false);

            var paperView = new PrescriptionPaperView();
            paperView.Populate(rx);

            // Assert empty sections are Collapsed
            Assert.Equal(Visibility.Collapsed, paperView.ObservationsBorderElement.Visibility);
            Assert.Equal(Visibility.Collapsed, paperView.AdviceBorderElement.Visibility);

            // Render isolated paper view to bitmap
            double width = PrintPreviewWindow.A4Width;
            double height = PrintPreviewWindow.A4Height;

            paperView.Width = width;
            paperView.Height = height;
            paperView.Measure(new Size(width, height));
            paperView.Arrange(new Rect(0, 0, width, height));
            paperView.UpdateLayout();

            int pixelWidth = (int)Math.Round(width);
            int pixelHeight = (int)Math.Round(height);

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(paperView);

            // Inspect the outermost border pixels: they must be pure white (no drop shadow on isolated view)
            var pixels = new uint[pixelWidth * pixelHeight];
            rtb.CopyPixels(pixels, pixelWidth * 4, 0);

            // Top-left pixel (0,0) and Top-right pixel (pixelWidth - 1, 0)
            uint topLeft = pixels[0];
            uint topRight = pixels[pixelWidth - 1];

            // 0xFFFFFFFF is solid white in Pbgra32
            Assert.Equal(0xFFFFFFFF, topLeft);
            Assert.Equal(0xFFFFFFFF, topRight);

            // Verify local time metadata string (no "Document Validated", contains local format)
            Assert.DoesNotContain("Document Validated", paperView.FooterMetadataTextElement.Text);
            Assert.Contains("Issued:", paperView.FooterMetadataTextElement.Text);
        });
    }

    [Fact]
    public void PrintPreviewWindow_PaperDimensions_AndAutoFitA4A5()
    {
        StaTestRunner.Run(() =>
        {
            var rx = CreateStubPrescription();
            var window = new PrintPreviewWindow(rx);

            // Initial selection is A4
            Assert.False(window.IsA5Selected);
            Assert.Equal(PrintPreviewWindow.A4Width, window.PaperContainerElement.Width);
            Assert.Equal(PrintPreviewWindow.A4Height, window.PaperContainerElement.Height);

            // Switch to A5
            window.PaperSizeSelectorElement.SelectedIndex = 1;
            Assert.True(window.IsA5Selected);
            Assert.Equal(PrintPreviewWindow.A5Width, window.PaperContainerElement.Width);
            Assert.Equal(PrintPreviewWindow.A5Height, window.PaperContainerElement.Height);

            // Zoom controls
            window.TriggerFitWidth();
            Assert.True(window.CurrentZoom > 0);

            window.TriggerZoomToFit();
            Assert.True(window.CurrentZoom > 0);

            window.Close();
        });
    }
}
