using System.Collections.Generic;

namespace DoctorRx.Application.Common;

public record DefaultCatalogEntry(
    string Name,
    string Category,
    string Subcategory,
    string? Abbreviation,
    string? SampleOrModality,
    string? DefaultInstructions
);

public static class DefaultInvestigationCatalogue
{
    public static readonly IReadOnlyList<DefaultCatalogEntry> InitialEntries = new List<DefaultCatalogEntry>
    {
        // ==========================================
        // 1. RADIOLOGY & MEDICAL IMAGING - X-RAY
        // ==========================================
        new("Chest X-Ray (PA View)", "Radiology & Imaging", "X-Ray", "CXR", "Digital Radiography", null),
        new("Chest X-Ray (PA & Lateral)", "Radiology & Imaging", "X-Ray", "CXR (PA/Lat)", "Digital Radiography", null),
        new("X-Ray Lumbar Spine (AP & Lateral)", "Radiology & Imaging", "X-Ray", "L-Spine X-Ray", "Digital Radiography", null),
        new("X-Ray Cervical Spine (AP & Lateral)", "Radiology & Imaging", "X-Ray", "C-Spine X-Ray", "Digital Radiography", null),
        new("X-Ray Knee Joint (AP & Lateral)", "Radiology & Imaging", "X-Ray", "Knee X-Ray", "Digital Radiography", null),
        new("X-Ray Pelvis (AP View)", "Radiology & Imaging", "X-Ray", "Pelvis X-Ray", "Digital Radiography", null),
        new("X-Ray Abdomen (Erect / Supine)", "Radiology & Imaging", "X-Ray", "AXR", "Digital Radiography", null),
        new("X-Ray Paranasal Sinuses (PNS - Water's View)", "Radiology & Imaging", "X-Ray", "PNS X-Ray", "Digital Radiography", null),
        new("X-Ray Foot (AP & Oblique)", "Radiology & Imaging", "X-Ray", "Foot X-Ray", "Digital Radiography", null),
        new("X-Ray Hand & Wrist (AP & Oblique)", "Radiology & Imaging", "X-Ray", "Hand/Wrist X-Ray", "Digital Radiography", null),
        new("X-Ray Shoulder Joint (AP & Axial)", "Radiology & Imaging", "X-Ray", "Shoulder X-Ray", "Digital Radiography", null),

        // ==========================================
        // 2. RADIOLOGY & MEDICAL IMAGING - ULTRASOUND
        // ==========================================
        new("Ultrasound Whole Abdomen", "Radiology & Imaging", "Ultrasound", "USG Abdomen", "B-Mode Ultrasound", null),
        new("Ultrasound Abdomen & Pelvis", "Radiology & Imaging", "Ultrasound", "USG Abd/Pelvis", "B-Mode Ultrasound", null),
        new("Ultrasound KUB (Kidney, Ureter, Bladder)", "Radiology & Imaging", "Ultrasound", "USG KUB", "B-Mode Ultrasound", null),
        new("Ultrasound Pelvis (Transabdominal)", "Radiology & Imaging", "Ultrasound", "USG Pelvis", "B-Mode Ultrasound", null),
        new("Ultrasound Neck & Thyroid", "Radiology & Imaging", "Ultrasound", "USG Thyroid", "High-Resolution Ultrasound", null),
        new("Ultrasound Scrotum & Testicular Doppler", "Radiology & Imaging", "Ultrasound", "USG Scrotum", "Color Doppler", null),
        new("Carotid Doppler Ultrasound", "Radiology & Imaging", "Ultrasound", "Carotid Doppler", "Duplex Ultrasound", null),
        new("Venous Doppler of Lower Limbs (Bilateral)", "Radiology & Imaging", "Ultrasound", "Venous Doppler", "Duplex Ultrasound", null),
        new("Obstetric Ultrasound (Routine / Anomaly)", "Radiology & Imaging", "Ultrasound", "Obs USG", "B-Mode & Doppler", null),

        // ==========================================
        // 3. RADIOLOGY & MEDICAL IMAGING - CT SCAN
        // ==========================================
        new("CT Brain / Head (Plain)", "Radiology & Imaging", "CT Scan", "CT Brain", "Multi-Detector CT", null),
        new("CT Brain / Head (Contrast Enhanced)", "Radiology & Imaging", "CT Scan", "CECT Brain", "Multi-Detector CT", null),
        new("HRCT Chest (High Resolution CT)", "Radiology & Imaging", "CT Scan", "HRCT Chest", "Thin-Section CT", null),
        new("CT Chest (Contrast Enhanced)", "Radiology & Imaging", "CT Scan", "CECT Chest", "Multi-Detector CT", null),
        new("CT Abdomen & Pelvis (Triple Phase / Contrast)", "Radiology & Imaging", "CT Scan", "CECT Abd/Pelvis", "Contrast Enhanced CT", null),
        new("CT KUB (Non-Contrast)", "Radiology & Imaging", "CT Scan", "NCCT KUB", "Low-Dose CT", null),
        new("CT Paranasal Sinuses (PNS - Coronal & Axial)", "Radiology & Imaging", "CT Scan", "CT PNS", "Bone Algorithm CT", null),
        new("CT Lumbar Spine", "Radiology & Imaging", "CT Scan", "CT L-Spine", "Multi-Detector CT", null),

        // ==========================================
        // 4. RADIOLOGY & MEDICAL IMAGING - MRI & OTHER
        // ==========================================
        new("MRI Brain (Plain & Contrast)", "Radiology & Imaging", "MRI", "MRI Brain", "1.5T / 3T MRI", null),
        new("MRI Lumbar Spine (L-Spine)", "Radiology & Imaging", "MRI", "MRI L-Spine", "1.5T / 3T MRI", null),
        new("MRI Cervical Spine (C-Spine)", "Radiology & Imaging", "MRI", "MRI C-Spine", "1.5T / 3T MRI", null),
        new("MRI Knee Joint", "Radiology & Imaging", "MRI", "MRI Knee", "1.5T / 3T MRI", null),
        new("MRI Shoulder Joint", "Radiology & Imaging", "MRI", "MRI Shoulder", "1.5T / 3T MRI", null),
        new("MRCP (Magnetic Resonance Cholangiopancreatography)", "Radiology & Imaging", "MRI", "MRCP", "Non-contrast MRI", null),
        new("Bilateral Digital Mammography", "Radiology & Imaging", "Mammography", "Mammogram", "Low-Dose X-Ray", null),
        new("DEXA Bone Mineral Density Scan", "Radiology & Imaging", "DEXA Scan", "DEXA BMD", "Dual-Energy X-Ray", null),

        // ==========================================
        // 5. LABORATORY - HEMATOLOGY & COAGULATION
        // ==========================================
        new("Complete Blood Count (CBC) with ESR", "Laboratory", "Hematology", "CBC + ESR", "EDTA Whole Blood", null),
        new("Complete Blood Count (CBC / CP)", "Laboratory", "Hematology", "CBC", "EDTA Whole Blood", null),
        new("Erythrocyte Sedimentation Rate (ESR)", "Laboratory", "Hematology", "ESR", "Sodium Citrate / EDTA", null),
        new("Peripheral Blood Film / Smear", "Laboratory", "Hematology", "PBF", "EDTA Blood Film", null),
        new("Blood Group & Rh Factor", "Laboratory", "Hematology", "Blood Grouping", "Whole Blood", null),
        new("Prothrombin Time & INR (PT / INR)", "Laboratory", "Hematology", "PT/INR", "Sodium Citrate Plasma", null),
        new("Activated Partial Thromboplastin Time (APTT)", "Laboratory", "Hematology", "APTT", "Sodium Citrate Plasma", null),
        new("Serum Ferritin", "Laboratory", "Hematology", "Ferritin", "Clotted Serum", null),
        new("Serum Vitamin B12 & Folate", "Laboratory", "Hematology", "Vit B12/Folate", "Clotted Serum", null),
        new("Hemoglobin Electrophoresis", "Laboratory", "Hematology", "Hb Electrophoresis", "EDTA Whole Blood", null),

        // ==========================================
        // 6. LABORATORY - BIOCHEMISTRY & METABOLIC
        // ==========================================
        new("Blood Glucose (Fasting)", "Laboratory", "Biochemistry", "FBS", "Fluoride Plasma / Serum", null),
        new("Blood Glucose (Random)", "Laboratory", "Biochemistry", "RBS", "Fluoride Plasma / Serum", null),
        new("Blood Glucose (2-Hour Post Prandial)", "Laboratory", "Biochemistry", "2-hr PPBS", "Fluoride Plasma / Serum", null),
        new("Glycated Hemoglobin (HbA1c)", "Laboratory", "Biochemistry", "HbA1c", "EDTA Whole Blood", null),
        new("Renal Function Tests (RFTs / KFT)", "Laboratory", "Biochemistry", "RFTs", "Clotted Serum", null),
        new("Serum Creatinine", "Laboratory", "Biochemistry", "Creatinine", "Clotted Serum", null),
        new("Blood Urea Nitrogen (BUN)", "Laboratory", "Biochemistry", "BUN", "Clotted Serum", null),
        new("Serum Uric Acid", "Laboratory", "Biochemistry", "Uric Acid", "Clotted Serum", null),
        new("Liver Function Tests (LFTs)", "Laboratory", "Biochemistry", "LFTs", "Clotted Serum", null),
        new("Lipid Profile", "Laboratory", "Biochemistry", "Lipid Profile", "Clotted Serum", null),
        new("Serum Electrolytes (Na+, K+, Cl-, HCO3-)", "Laboratory", "Biochemistry", "Electrolytes", "Clotted Serum", null),
        new("Serum Calcium & Phosphate", "Laboratory", "Biochemistry", "Ca/PO4", "Clotted Serum", null),
        new("Serum Albumin & Total Protein", "Laboratory", "Biochemistry", "Albumin/TP", "Clotted Serum", null),
        new("Serum Amylase & Lipase", "Laboratory", "Biochemistry", "Amylase/Lipase", "Clotted Serum", null),
        new("High-Sensitivity Cardiac Troponin I", "Laboratory", "Biochemistry", "hs-cTnI", "Heparin Plasma / Serum", null),
        new("Serum Creatine Kinase (CK-MB)", "Laboratory", "Biochemistry", "CK-MB", "Clotted Serum", null),
        new("Arterial Blood Gas Analysis (ABG)", "Laboratory", "Biochemistry", "ABG", "Heparinized Arterial Blood", null),

        // ==========================================
        // 7. LABORATORY - ENDOCRINE & IMMUNOLOGY
        // ==========================================
        new("Thyroid Profile (TSH, Free T3, Free T4)", "Laboratory", "Endocrinology", "TFTs", "Clotted Serum", null),
        new("Serum TSH (Ultrasensitive)", "Laboratory", "Endocrinology", "TSH", "Clotted Serum", null),
        new("Serum 25-Hydroxy Vitamin D3", "Laboratory", "Endocrinology", "Vit D3", "Clotted Serum", null),
        new("Serum Beta-hCG (Quantitative)", "Laboratory", "Endocrinology", "Beta-hCG", "Clotted Serum", null),
        new("Serum Prostate Specific Antigen (Total PSA)", "Laboratory", "Endocrinology", "Total PSA", "Clotted Serum", null),
        new("C-Reactive Protein (CRP - Quantitative)", "Laboratory", "Immunology", "CRP", "Clotted Serum", null),
        new("Rheumatoid Factor (RA Factor)", "Laboratory", "Immunology", "RF", "Clotted Serum", null),
        new("Anti-Cyclic Citrullinated Peptide (Anti-CCP)", "Laboratory", "Immunology", "Anti-CCP", "Clotted Serum", null),
        new("Antinuclear Antibodies (ANA by IFA)", "Laboratory", "Immunology", "ANA", "Clotted Serum", null),
        new("Anti-dsDNA Antibodies", "Laboratory", "Immunology", "Anti-dsDNA", "Clotted Serum", null),

        // ==========================================
        // 8. LABORATORY - URINE, STOOL & MICROBIOLOGY
        // ==========================================
        new("Urine Routine Examination (Urine R/E)", "Laboratory", "Clinical Pathology", "Urine R/E", "Fresh Midstream Urine", null),
        new("Urine Culture & Sensitivity (C&S)", "Laboratory", "Microbiology", "Urine C&S", "Sterile Midstream Urine", null),
        new("Stool Routine Examination (Stool R/E)", "Laboratory", "Clinical Pathology", "Stool R/E", "Fresh Stool Specimen", null),
        new("Stool for Occult Blood (FOBT)", "Laboratory", "Clinical Pathology", "FOBT", "Stool Specimen", null),
        new("Blood Culture & Sensitivity (Bilateral)", "Laboratory", "Microbiology", "Blood C&S", "Venous Blood in Culture Bottles", null),
        new("Blood CP for Malarial Parasite (MP / ICT)", "Laboratory", "Microbiology", "MP Test", "Whole Blood (EDTA)", null),
        new("Typhoid Serology (Typhidot IgM & IgG)", "Laboratory", "Serology", "Typhidot", "Clotted Serum", null),
        new("Dengue Serology (NS1 Antigen, IgM, IgG)", "Laboratory", "Serology", "Dengue Duo", "Clotted Serum", null),
        new("HBsAg Screening (ELISA / ECLIA)", "Laboratory", "Serology", "HBsAg", "Clotted Serum", null),
        new("Anti-HCV Screening (ELISA / ECLIA)", "Laboratory", "Serology", "Anti-HCV", "Clotted Serum", null),
        new("HIV 1 & 2 Screening Test", "Laboratory", "Serology", "HIV 1/2", "Clotted Serum", null),

        // ==========================================
        // 9. OTHER DIAGNOSTIC INVESTIGATIONS
        // ==========================================
        new("12-Lead Electrocardiogram (ECG)", "Cardiology & Diagnostics", "Cardiology", "ECG", "Electrophysiology", null),
        new("Transthoracic Echocardiography (ECHO)", "Cardiology & Diagnostics", "Cardiology", "2D Echo", "Cardiac Ultrasound", null),
        new("Exercise Tolerance Test (ETT / TMT)", "Cardiology & Diagnostics", "Cardiology", "ETT", "Treadmill Stress Test", null),
        new("24-Hour Ambulatory Holter ECG Monitoring", "Cardiology & Diagnostics", "Cardiology", "Holter", "Ambulatory ECG", null),
        new("24-Hour Ambulatory Blood Pressure Monitoring", "Cardiology & Diagnostics", "Cardiology", "ABPM", "Ambulatory Cuff Monitor", null),
        new("Spirometry / Pulmonary Function Test (PFT)", "Cardiology & Diagnostics", "Pulmonology", "PFT", "Flow-Volume Loop", null),
        new("Electroencephalogram (EEG - Routine)", "Cardiology & Diagnostics", "Neurology", "EEG", "Electrophysiology", null),
        new("Upper GI Endoscopy (EGD)", "Cardiology & Diagnostics", "Endoscopy", "EGD", "Endoscopic Examination", null),
        new("Colonoscopy", "Cardiology & Diagnostics", "Endoscopy", "Colonoscopy", "Endoscopic Examination", null)
    };
}
