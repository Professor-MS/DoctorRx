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
        new("Chest X-Ray (PA View)", "Radiology & Imaging", "X-Ray", "CXR", "Digital Radiography", "Deep inspiration required"),
        new("Chest X-Ray (PA & Lateral)", "Radiology & Imaging", "X-Ray", "CXR (PA/Lat)", "Digital Radiography", "Full inspiration, erect posture"),
        new("X-Ray Lumbar Spine (AP & Lateral)", "Radiology & Imaging", "X-Ray", "L-Spine X-Ray", "Digital Radiography", "Remove radiopaque objects"),
        new("X-Ray Cervical Spine (AP & Lateral)", "Radiology & Imaging", "X-Ray", "C-Spine X-Ray", "Digital Radiography", "Include open-mouth odontoid view if indicated"),
        new("X-Ray Knee Joint (AP & Lateral)", "Radiology & Imaging", "X-Ray", "Knee X-Ray", "Digital Radiography", "Weight-bearing AP preferred"),
        new("X-Ray Pelvis (AP View)", "Radiology & Imaging", "X-Ray", "Pelvis X-Ray", "Digital Radiography", "Standard supine AP"),
        new("X-Ray Abdomen (Erect / Supine)", "Radiology & Imaging", "X-Ray", "AXR", "Digital Radiography", "Check for air-fluid levels / free air"),
        new("X-Ray Paranasal Sinuses (PNS - Water's View)", "Radiology & Imaging", "X-Ray", "PNS X-Ray", "Digital Radiography", "Water's view for maxillary sinuses"),
        new("X-Ray Foot (AP & Oblique)", "Radiology & Imaging", "X-Ray", "Foot X-Ray", "Digital Radiography", "Weight-bearing if tolerable"),
        new("X-Ray Hand & Wrist (AP & Oblique)", "Radiology & Imaging", "X-Ray", "Hand/Wrist X-Ray", "Digital Radiography", "Examine carpal bones & joints"),
        new("X-Ray Shoulder Joint (AP & Axial)", "Radiology & Imaging", "X-Ray", "Shoulder X-Ray", "Digital Radiography", "True AP and axillary view"),

        // ==========================================
        // 2. RADIOLOGY & MEDICAL IMAGING - ULTRASOUND
        // ==========================================
        new("Ultrasound Whole Abdomen", "Radiology & Imaging", "Ultrasound", "USG Abdomen", "B-Mode Ultrasound", "6-8 hours fasting required"),
        new("Ultrasound Abdomen & Pelvis", "Radiology & Imaging", "Ultrasound", "USG Abd/Pelvis", "B-Mode Ultrasound", "Fasting for abdomen + full bladder for pelvis"),
        new("Ultrasound KUB (Kidney, Ureter, Bladder)", "Radiology & Imaging", "Ultrasound", "USG KUB", "B-Mode Ultrasound", "Full bladder required for pre & post void"),
        new("Ultrasound Pelvis (Transabdominal)", "Radiology & Imaging", "Ultrasound", "USG Pelvis", "B-Mode Ultrasound", "Full bladder required"),
        new("Ultrasound Neck & Thyroid", "Radiology & Imaging", "Ultrasound", "USG Thyroid", "High-Resolution Ultrasound", "No special preparation"),
        new("Ultrasound Scrotum & Testicular Doppler", "Radiology & Imaging", "Ultrasound", "USG Scrotum", "Color Doppler", "No special preparation"),
        new("Carotid Doppler Ultrasound", "Radiology & Imaging", "Ultrasound", "Carotid Doppler", "Duplex Ultrasound", "Bilateral carotid evaluation"),
        new("Venous Doppler of Lower Limbs (Bilateral)", "Radiology & Imaging", "Ultrasound", "Venous Doppler", "Duplex Ultrasound", "Evaluate for DVT / insufficiency"),
        new("Obstetric Ultrasound (Routine / Anomaly)", "Radiology & Imaging", "Ultrasound", "Obs USG", "B-Mode & Doppler", "Bring previous dating scans"),

        // ==========================================
        // 3. RADIOLOGY & MEDICAL IMAGING - CT SCAN
        // ==========================================
        new("CT Brain / Head (Plain)", "Radiology & Imaging", "CT Scan", "CT Brain", "Multi-Detector CT", "Plain non-contrast scan"),
        new("CT Brain / Head (Contrast Enhanced)", "Radiology & Imaging", "CT Scan", "CECT Brain", "Multi-Detector CT", "Serum creatinine check + 4 hours fasting"),
        new("HRCT Chest (High Resolution CT)", "Radiology & Imaging", "CT Scan", "HRCT Chest", "Thin-Section CT", "Inspiratory & expiratory imaging"),
        new("CT Chest (Contrast Enhanced)", "Radiology & Imaging", "CT Scan", "CECT Chest", "Multi-Detector CT", "Creatinine test required for IV contrast"),
        new("CT Abdomen & Pelvis (Triple Phase / Contrast)", "Radiology & Imaging", "CT Scan", "CECT Abd/Pelvis", "Contrast Enhanced CT", "4-6 hours fasting + normal renal function"),
        new("CT KUB (Non-Contrast)", "Radiology & Imaging", "CT Scan", "NCCT KUB", "Low-Dose CT", "No IV contrast, check for calculi"),
        new("CT Paranasal Sinuses (PNS - Coronal & Axial)", "Radiology & Imaging", "CT Scan", "CT PNS", "Bone Algorithm CT", "Non-contrast fine slices"),
        new("CT Lumbar Spine", "Radiology & Imaging", "CT Scan", "CT L-Spine", "Multi-Detector CT", "Bone & disc evaluation"),

        // ==========================================
        // 4. RADIOLOGY & MEDICAL IMAGING - MRI & OTHER
        // ==========================================
        new("MRI Brain (Plain & Contrast)", "Radiology & Imaging", "MRI", "MRI Brain", "1.5T / 3T MRI", "Remove metallic objects, check pacemaker/implants"),
        new("MRI Lumbar Spine (L-Spine)", "Radiology & Imaging", "MRI", "MRI L-Spine", "1.5T / 3T MRI", "Sagittal & axial T1/T2 disc imaging"),
        new("MRI Cervical Spine (C-Spine)", "Radiology & Imaging", "MRI", "MRI C-Spine", "1.5T / 3T MRI", "Cord & nerve root evaluation"),
        new("MRI Knee Joint", "Radiology & Imaging", "MRI", "MRI Knee", "1.5T / 3T MRI", "Evaluate menisci & cruciate ligaments"),
        new("MRI Shoulder Joint", "Radiology & Imaging", "MRI", "MRI Shoulder", "1.5T / 3T MRI", "Rotator cuff and labrum evaluation"),
        new("MRCP (Magnetic Resonance Cholangiopancreatography)", "Radiology & Imaging", "MRI", "MRCP", "Non-contrast MRI", "6 hours fasting required"),
        new("Bilateral Digital Mammography", "Radiology & Imaging", "Mammography", "Mammogram", "Low-Dose X-Ray", "Best done post-menstrual week"),
        new("DEXA Bone Mineral Density Scan", "Radiology & Imaging", "DEXA Scan", "DEXA BMD", "Dual-Energy X-Ray", "Hip & spine T-score assessment"),

        // ==========================================
        // 5. LABORATORY - HEMATOLOGY & COAGULATION
        // ==========================================
        new("Complete Blood Count (CBC) with ESR", "Laboratory", "Hematology", "CBC + ESR", "EDTA Whole Blood", "Routine venipuncture"),
        new("Complete Blood Count (CBC / CP)", "Laboratory", "Hematology", "CBC", "EDTA Whole Blood", "Includes Hb, TLC, DLC, Platelets"),
        new("Erythrocyte Sedimentation Rate (ESR)", "Laboratory", "Hematology", "ESR", "Sodium Citrate / EDTA", "Westergren method"),
        new("Peripheral Blood Film / Smear", "Laboratory", "Hematology", "PBF", "EDTA Blood Film", "Microscopic morphology assessment"),
        new("Blood Group & Rh Factor", "Laboratory", "Hematology", "Blood Grouping", "Whole Blood", "ABO and Rhesus determination"),
        new("Prothrombin Time & INR (PT / INR)", "Laboratory", "Hematology", "PT/INR", "Sodium Citrate Plasma", "Anticoagulation monitoring"),
        new("Activated Partial Thromboplastin Time (APTT)", "Laboratory", "Hematology", "APTT", "Sodium Citrate Plasma", "Intrinsic pathway screening"),
        new("Serum Ferritin", "Laboratory", "Hematology", "Ferritin", "Clotted Serum", "Iron storage assessment"),
        new("Serum Vitamin B12 & Folate", "Laboratory", "Hematology", "Vit B12/Folate", "Clotted Serum", "Fasting sample preferred"),
        new("Hemoglobin Electrophoresis", "Laboratory", "Hematology", "Hb Electrophoresis", "EDTA Whole Blood", "Thalassemia & hemoglobinopathy workup"),

        // ==========================================
        // 6. LABORATORY - BIOCHEMISTRY & METABOLIC
        // ==========================================
        new("Blood Glucose (Fasting)", "Laboratory", "Biochemistry", "FBS", "Fluoride Plasma / Serum", "Strict 8-10 hours fasting"),
        new("Blood Glucose (Random)", "Laboratory", "Biochemistry", "RBS", "Fluoride Plasma / Serum", "Any time random sample"),
        new("Blood Glucose (2-Hour Post Prandial)", "Laboratory", "Biochemistry", "2-hr PPBS", "Fluoride Plasma / Serum", "Exactly 2 hours after meal"),
        new("Glycated Hemoglobin (HbA1c)", "Laboratory", "Biochemistry", "HbA1c", "EDTA Whole Blood", "Fasting not required, 3-month glycemic control"),
        new("Renal Function Tests (RFTs / KFT)", "Laboratory", "Biochemistry", "RFTs", "Clotted Serum", "Urea, Creatinine, eGFR, BUN"),
        new("Serum Creatinine", "Laboratory", "Biochemistry", "Creatinine", "Clotted Serum", "Renal filtration marker"),
        new("Blood Urea Nitrogen (BUN)", "Laboratory", "Biochemistry", "BUN", "Clotted Serum", "Renal clearance marker"),
        new("Serum Uric Acid", "Laboratory", "Biochemistry", "Uric Acid", "Clotted Serum", "Gout & hyperuricemia screening"),
        new("Liver Function Tests (LFTs)", "Laboratory", "Biochemistry", "LFTs", "Clotted Serum", "Bilirubin (T/D), SGPT/ALT, SGOT/AST, ALP"),
        new("Lipid Profile", "Laboratory", "Biochemistry", "Lipid Profile", "Clotted Serum", "Strict 10-12 hours fasting (Chol, Trig, HDL, LDL)"),
        new("Serum Electrolytes (Na+, K+, Cl-, HCO3-)", "Laboratory", "Biochemistry", "Electrolytes", "Clotted Serum", "Fluid and acid-base status"),
        new("Serum Calcium & Phosphate", "Laboratory", "Biochemistry", "Ca/PO4", "Clotted Serum", "Bone & parathyroid evaluation"),
        new("Serum Albumin & Total Protein", "Laboratory", "Biochemistry", "Albumin/TP", "Clotted Serum", "Nutritional and hepatic status"),
        new("Serum Amylase & Lipase", "Laboratory", "Biochemistry", "Amylase/Lipase", "Clotted Serum", "Acute pancreatitis evaluation"),
        new("High-Sensitivity Cardiac Troponin I", "Laboratory", "Biochemistry", "hs-cTnI", "Heparin Plasma / Serum", "Acute coronary syndrome evaluation"),
        new("Serum Creatine Kinase (CK-MB)", "Laboratory", "Biochemistry", "CK-MB", "Clotted Serum", "Myocardial injury marker"),
        new("Arterial Blood Gas Analysis (ABG)", "Laboratory", "Biochemistry", "ABG", "Heparinized Arterial Blood", "Immediate ice transport"),

        // ==========================================
        // 7. LABORATORY - ENDOCRINE & IMMUNOLOGY
        // ==========================================
        new("Thyroid Profile (TSH, Free T3, Free T4)", "Laboratory", "Endocrinology", "TFTs", "Clotted Serum", "Thyroid functional status"),
        new("Serum TSH (Ultrasensitive)", "Laboratory", "Endocrinology", "TSH", "Clotted Serum", "Primary thyroid screening"),
        new("Serum 25-Hydroxy Vitamin D3", "Laboratory", "Endocrinology", "Vit D3", "Clotted Serum", "Vitamin D deficiency assessment"),
        new("Serum Beta-hCG (Quantitative)", "Laboratory", "Endocrinology", "Beta-hCG", "Clotted Serum", "Pregnancy & trophoblastic marker"),
        new("Serum Prostate Specific Antigen (Total PSA)", "Laboratory", "Endocrinology", "Total PSA", "Clotted Serum", "Prostatic screening (avoid pre-exam manipulation)"),
        new("C-Reactive Protein (CRP - Quantitative)", "Laboratory", "Immunology", "CRP", "Clotted Serum", "Acute phase systemic inflammation"),
        new("Rheumatoid Factor (RA Factor)", "Laboratory", "Immunology", "RF", "Clotted Serum", "Rheumatoid arthritis workup"),
        new("Anti-Cyclic Citrullinated Peptide (Anti-CCP)", "Laboratory", "Immunology", "Anti-CCP", "Clotted Serum", "Specific rheumatoid arthritis marker"),
        new("Antinuclear Antibodies (ANA by IFA)", "Laboratory", "Immunology", "ANA", "Clotted Serum", "Autoimmune & connective tissue disease workup"),
        new("Anti-dsDNA Antibodies", "Laboratory", "Immunology", "Anti-dsDNA", "Clotted Serum", "Systemic lupus erythematosus workup"),

        // ==========================================
        // 8. LABORATORY - URINE, STOOL & MICROBIOLOGY
        // ==========================================
        new("Urine Routine Examination (Urine R/E)", "Laboratory", "Clinical Pathology", "Urine R/E", "Fresh Midstream Urine", "Clean-catch midstream sample"),
        new("Urine Culture & Sensitivity (C&S)", "Laboratory", "Microbiology", "Urine C&S", "Sterile Midstream Urine", "Prior to initiating antibiotics"),
        new("Stool Routine Examination (Stool R/E)", "Laboratory", "Clinical Pathology", "Stool R/E", "Fresh Stool Specimen", "Examine for ova, cysts, occult blood"),
        new("Stool for Occult Blood (FOBT)", "Laboratory", "Clinical Pathology", "FOBT", "Stool Specimen", "Avoid red meat 48 hrs prior"),
        new("Blood Culture & Sensitivity (Bilateral)", "Laboratory", "Microbiology", "Blood C&S", "Venous Blood in Culture Bottles", "Aseptic technique, at peak fever"),
        new("Blood CP for Malarial Parasite (MP / ICT)", "Laboratory", "Microbiology", "MP Test", "Whole Blood (EDTA)", "Smear and rapid antigen test"),
        new("Typhoid Serology (Typhidot IgM & IgG)", "Laboratory", "Serology", "Typhidot", "Clotted Serum", "Enteric fever screening"),
        new("Dengue Serology (NS1 Antigen, IgM, IgG)", "Laboratory", "Serology", "Dengue Duo", "Clotted Serum", "Dengue fever detection"),
        new("HBsAg Screening (ELISA / ECLIA)", "Laboratory", "Serology", "HBsAg", "Clotted Serum", "Hepatitis B viral screening"),
        new("Anti-HCV Screening (ELISA / ECLIA)", "Laboratory", "Serology", "Anti-HCV", "Clotted Serum", "Hepatitis C viral screening"),
        new("HIV 1 & 2 Screening Test", "Laboratory", "Serology", "HIV 1/2", "Clotted Serum", "Confidential screening"),

        // ==========================================
        // 9. OTHER DIAGNOSTIC INVESTIGATIONS
        // ==========================================
        new("12-Lead Electrocardiogram (ECG)", "Cardiology & Diagnostics", "Cardiology", "ECG", "Electrophysiology", "Resting 12-lead trace"),
        new("Transthoracic Echocardiography (ECHO)", "Cardiology & Diagnostics", "Cardiology", "2D Echo", "Cardiac Ultrasound", "Resting cardiac structure and Doppler assessment"),
        new("Exercise Tolerance Test (ETT / TMT)", "Cardiology & Diagnostics", "Cardiology", "ETT", "Treadmill Stress Test", "Comfortable walking shoes, hold beta blockers if instructed"),
        new("24-Hour Ambulatory Holter ECG Monitoring", "Cardiology & Diagnostics", "Cardiology", "Holter", "Ambulatory ECG", "Maintain daily activity log, keep unit dry"),
        new("24-Hour Ambulatory Blood Pressure Monitoring", "Cardiology & Diagnostics", "Cardiology", "ABPM", "Ambulatory Cuff Monitor", "Wear loose sleeve clothing"),
        new("Spirometry / Pulmonary Function Test (PFT)", "Cardiology & Diagnostics", "Pulmonology", "PFT", "Flow-Volume Loop", "Hold short-acting bronchodilators 6 hrs prior"),
        new("Electroencephalogram (EEG - Routine)", "Cardiology & Diagnostics", "Neurology", "EEG", "Electrophysiology", "Clean, dry hair, sleep deprived if requested"),
        new("Upper GI Endoscopy (EGD)", "Cardiology & Diagnostics", "Endoscopy", "EGD", "Endoscopic Examination", "Strict fasting from midnight, arrange escort"),
        new("Colonoscopy", "Cardiology & Diagnostics", "Endoscopy", "Colonoscopy", "Endoscopic Examination", "Full bowel preparation as per protocol")
    };
}
