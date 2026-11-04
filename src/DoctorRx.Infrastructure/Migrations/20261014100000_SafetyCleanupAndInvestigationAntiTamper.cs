using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SafetyCleanupAndInvestigationAntiTamper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add IsSealed, CancellationReason, and CancelledAtUtc to InvestigationRequests
            migrationBuilder.AddColumn<bool>(
                name: "IsSealed",
                table: "InvestigationRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "InvestigationRequests",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "InvestigationRequests",
                type: "TEXT",
                nullable: true);

            // 2. Clear auto-generated medicine descriptions matching old catalog text
            migrationBuilder.Sql(@"
UPDATE Medicines
SET Description = NULL
WHERE Description IN (
    'Pain and fever relief (headaches, muscular aches, feverish colds)',
    'Pain and fever relief (headaches, body aches, dental pain)',
    'Fever and mild-to-moderate pain in infants and children',
    'Headache, toothache, fever & cardiovascular antiplatelet protection',
    'Pain, inflammation, osteoarthritis, rheumatoid arthritis & fever',
    'Pain, inflammation, joint stiffness, sprains & fever',
    'Acute musculoskeletal pain, arthritis, sprains & post-traumatic inflammation',
    'Inflammatory pain, rheumatoid arthritis & osteoarthritis',
    'Long-acting relief for joint stiffness, arthritis & acute inflammatory pain',
    'Primary dysmenorrhea (menstrual cramps), dental pain & acute pain relief',
    'Primary dysmenorrhea (menstrual pain), toothaches & surgical pain',
    'Osteoarthritis, rheumatoid arthritis & acute pain with lower GI risk',
    'Cold, flu, nasal congestion, fever & sinus pain relief',
    'Relief of cold, flu symptoms, sinus pressure & nasal congestion',
    'Bronchial asthma cough, asthmatic bronchitis & chest tightness',
    'Allergic rhinitis (hay fever), sneezing, runny nose & chronic urticaria (hives)',
    'Allergy symptoms, hay fever & itchy skin conditions',
    'Allergic rhinitis, allergic conjunctivitis & urticaria symptom relief',
    'Seasonal allergic rhinitis, sneezing, nasal itching & hives',
    'Seasonal allergic rhinitis (hay fever) & chronic idiopathic urticaria',
    'Hay fever, skin allergies & chronic hives relief',
    'Allergy symptom relief, itching, runny nose & allergic skin reactions',
    'Emergency and acute allergic reaction symptom control & itching relief',
    'Persistent asthma maintenance & seasonal allergic rhinitis',
    'Acute asthma attacks, bronchospasm, wheezing & exercise-induced asthma',
    'Relief of bronchospasm in bronchial asthma, bronchitis & COPD',
    'Maintenance control of chronic bronchial asthma & airway inflammation',
    'Prophylactic management of persistent asthma in adults and children',
    'COPD bronchospasm & acute asthma exacerbation adjunct',
    'Chronic obstructive pulmonary disease & persistent bronchospasm',
    'Acid reflux (GERD), heartburn, gastric & duodenal peptic ulcers',
    'Heartburn, acid reflux (GERD) & peptic ulcer healing',
    'Gastroesophageal reflux, erosive esophagitis & peptic ulcer disease',
    'Gastroesophageal reflux disease (GERD), erosive esophagitis & peptic ulcers',
    'Erosive esophagitis healing, acid reflux & NSAID-associated ulcer prevention',
    'Acid-related disorders, erosive esophagitis & Zollinger-Ellison syndrome',
    'Heartburn, acid indigestion & gastric hypersecretion relief',
    'Chemotherapy, radiation & postoperative nausea and vomiting prevention',
    'Prevention and treatment of dehydration caused by acute diarrhea',
    'Symptomatic control of acute non-infectious diarrhea & traveler''s diarrhea',
    'Acute diarrhea symptom control and reduction of bowel movements',
    'Chronic constipation relief & portal-systemic encephalopathy management',
    'Type 2 diabetes first-line glycemic control & insulin resistance',
    'Type 2 diabetes blood glucose regulation & insulin resistance',
    'Type 2 diabetes glycemic control in non-obese or elderly patients',
    'Type 2 diabetes blood sugar reduction when diet alone is insufficient',
    'Type 2 diabetes blood sugar reduction in combination with diet and exercise',
    'Type 2 diabetes glycemic control to lower blood glucose levels',
    'Type 2 diabetes blood glucose regulation with low risk of hypoglycemia',
    'Glycemic control improvement in patients with type 2 diabetes',
    'Type 2 diabetes blood sugar control, heart failure & kidney protection',
    'Type 2 diabetes glycemic control and cardiovascular mortality risk reduction',
    'Hypertension (high blood pressure) & chronic stable angina',
    'Essential hypertension & coronary artery disease (angina) management',
    'Hypertension & reduction of renal disease progression in diabetic patients',
    'High blood pressure control & cardiovascular stroke risk reduction',
    'Hypertension, chronic heart failure & asymptomatic left ventricular dysfunction',
    'Hypertension, angina pectoris & stable chronic heart failure',
    'High blood pressure control & ischemic heart disease management',
    'Essential hypertension & edema associated with congestive heart failure',
    'Edema in heart failure, acute pulmonary edema & fluid overload',
    'Fluid retention, congestive heart failure & renal edema relief',
    'High cholesterol, hyperlipidemia & prevention of cardiovascular events',
    'Elevated LDL cholesterol reduction & atherosclerotic cardiovascular disease prevention',
    'Severe hypercholesterolemia, high triglycerides & cardiovascular risk reduction',
    'Primary hyperlipidemia treatment & slowing atherosclerosis progression',
    'Prevention of stroke, myocardial infarction & blood clots in arterial disease',
    'Atherothrombotic event prevention in patients with acute coronary syndrome',
    'Bacterial chest, ear, nose, throat, dental & urinary tract infections',
    'Bacterial respiratory, ENT & uncomplicated genitourinary infections',
    'Resistant respiratory, skin, soft tissue & dental bacterial infections',
    'Severe respiratory, ENT, skin, urinary tract & soft tissue bacterial infections',
    'Community-acquired pneumonia, acute bronchitis, sinusitis & throat infections',
    'Bacterial respiratory tract infections, skin infections & ear infections',
    'H. pylori stomach infection eradication, pneumonia & skin bacterial infections',
    'Upper and lower respiratory tract infections & peptic ulcer H. pylori regimens',
    'Typhoid fever, bacterial bronchitis, otitis media & uncomplicated UTIs',
    'Bacterial upper respiratory, ENT & urinary tract infections',
    'Skin, soft tissue, bone, dental & respiratory bacterial infections',
    'Bacterial infections of the skin, bone, respiratory tract & urinary tract',
    'Atypical pneumonia, severe acne vulgaris, pelvic infections & malaria prevention',
    'Bacterial respiratory infections, severe acne, chlamydia & rickettsial diseases',
    'Anaerobic bacterial infections, dental abscesses, amebiasis & giardiasis',
    'Intestinal amoebiasis, giardiasis, trichomoniasis & anaerobic bacterial infections',
    'Complicated urinary tract infections, severe diarrhea & bone/joint infections',
    'Severe bacterial urinary tract, gastrointestinal & intra-abdominal infections',
    'Acute uncomplicated lower urinary tract infections (cystitis) & UTI prophylaxis',
    'Vaginal yeast infections (candidiasis), oral thrush & systemic fungal infections',
    'Mucosal candidiasis, vaginal thrush & systemic fungal disease prevention',
    'Athlete''s foot, jock itch, ringworm & fungal skin candidiasis',
    'Fungal skin infections, candidal intertrigo & tinea pedis',
    'Tinea pedis (athlete''s foot), tinea cruris & stubborn fungal skin infections',
    'Fungal skin infections, ringworm & athlete''s foot',
    'Herpes simplex, genital herpes, shingles (herpes zoster) & chickenpox',
    'Cold sores, genital herpes virus flare-ups & shingles treatment',
    'Impetigo, folliculitis & superficial bacterial skin infections',
    'Bacterial impetigo, infected eczema & minor traumatic skin wounds',
    'Mild eczema, contact dermatitis, insect stings & skin redness/itching',
    'Severe eczema, psoriasis, neurodermatitis & lichen planus',
    'Inflammatory eczema, stubborn dermatitis & psoriasis flare-ups',
    'Scabies infestation & crab lice eradication in adults and children',
    'Eradication of human scabies mites and associated nocturnal itching',
    'Acne vulgaris, inflammatory pimples, blackheads & blemishes',
    'Seborrheic dermatitis, stubborn dandruff & fungal skin infections',
    'Dandruff, seborrheic dermatitis & pityriasis versicolor on scalp',
    'Diaper rash, minor skin burns, chafing & soothing barrier protection',
    'Diaper rash, eczema, minor abrasions & bedsores protection',
    'Chronic open-angle glaucoma & lowering elevated intraocular pressure',
    'Open-angle glaucoma & ocular hypertension intraocular pressure reduction',
    'Elevated intraocular pressure in open-angle glaucoma patients',
    'Acute bacterial conjunctivitis & superficial bacterial eye infections',
    'Impacted earwax softening, loosening and gentle removal',
    'Bacterial otitis externa, ear canal infections & chronic otitis media',
    'Hypothyroidism (underactive thyroid) hormone replacement therapy',
    'Underactive thyroid gland substitution therapy & goiter suppression',
    'Severe asthma flares, allergic conditions & autoimmune inflammation',
    'Chronic gout prevention, hyperuricemia & urate kidney stones',
    'Gouty arthritis prevention & hyperuricemia in adults',
    'Acute gout flares pain relief and familial Mediterranean fever',
    'Benign prostatic hyperplasia (BPH) urinary obstruction & hesitancy relief',
    'Lower urinary tract symptoms associated with enlarged prostate (BPH)',
    'Benign prostatic hyperplasia (BPH) prostate enlargement reduction',
    'Benign prostatic hyperplasia symptoms and urinary retention risk reduction',
    'Major depressive disorder, panic disorder, obsessive-compulsive disorder & PTSD',
    'Depression, generalized anxiety disorder, panic attacks & social phobia',
    'Major depressive disorder, bulimia nervosa & obsessive-compulsive disorder',
    'Clinical depression, obsessive-compulsive disorder & panic episodes',
    'Depression, chronic neuropathic nerve pain & migraine prophylaxis',
    'Short-term acute anxiety, muscle spasm relief & acute seizure management',
    'Severe acute anxiety, agitation & skeletal muscle spasms',
    'Partial-onset seizures, myoclonic seizures & generalized tonic-clonic epilepsy',
    'Seizure control in epilepsy patients as monotherapy or adjunctive therapy',
    'Generalized and partial epilepsy seizures & bipolar disorder mania',
    'Epileptic seizures control & bipolar affective disorder prophylaxis',
    'Epileptic seizures & severe trigeminal facial nerve neuralgia',
    'Partial and secondary generalized seizures & trigeminal neuralgia',
    'Diabetic neuropathy, postherpetic nerve pain & focal seizures',
    'Peripheral neuropathic pain relief & adjunctive seizure therapy',
    'Diabetic neuropathic pain, postherpetic neuralgia & fibromyalgia',
    'Neuropathic pain, fibromyalgia & generalized anxiety disorder',
    'Iron deficiency anemia treatment and replenishment of iron stores',
    'Iron and folate deficiency treatment, especially during pregnancy',
    'Folate deficiency anemia & neural tube defect prevention in pregnancy',
    'Vitamin D deficiency treatment, bone mineral health & osteoporosis support',
    'Correction of vitamin D deficiency and healthy bone maintenance',
    'Adjunct treatment in pediatric acute diarrhea & zinc deficiency',
    'Diarrhea recovery acceleration in children & trace element supplementation',
    'Life-threatening acute anaphylactic shock & severe allergic emergencies',
    'Emergency treatment of severe allergic anaphylaxis & cardiac arrest',
    'Local infiltration anesthesia for minor surgical & dental procedures',
    'Local and regional nerve block anesthesia for procedural pain control',
    'Clinical indication as directed by physician'
);
");

            // 3. Null seeded default instructions in InvestigationCatalogItems
            migrationBuilder.Sql(@"
UPDATE InvestigationCatalogItems
SET DefaultInstructions = NULL
WHERE DefaultInstructions IN (
    'Deep inspiration required',
    'Full inspiration, erect posture',
    'Remove radiopaque objects',
    'Include open-mouth odontoid view if indicated',
    'Weight-bearing AP preferred',
    'Standard supine AP',
    'Check for air-fluid levels / free air',
    'Water''s view for maxillary sinuses',
    'Weight-bearing if tolerable',
    'Examine carpal bones & joints',
    'True AP and axillary view',
    '6-8 hours fasting required',
    'Fasting for abdomen + full bladder for pelvis',
    'Full bladder required for pre & post void',
    'Full bladder required',
    'No special preparation',
    'Bilateral carotid evaluation',
    'Evaluate for DVT / insufficiency',
    'Bring previous dating scans',
    'Plain non-contrast scan',
    'Serum creatinine check + 4 hours fasting',
    'Inspiratory & expiratory imaging',
    'Creatinine test required for IV contrast',
    '4-6 hours fasting + normal renal function',
    'No IV contrast, check for calculi',
    'Non-contrast fine slices',
    'Bone & disc evaluation',
    'Remove metallic objects, check pacemaker/implants',
    'Sagittal & axial T1/T2 disc imaging',
    'Cord & nerve root evaluation',
    'Evaluate menisci & cruciate ligaments',
    'Rotator cuff and labrum evaluation',
    'Non-contrast MRI',
    'Best done post-menstrual week',
    'Hip & spine T-score assessment',
    'Routine venipuncture',
    'Includes Hb, TLC, DLC, Platelets',
    'Westergren method',
    'Microscopic morphology assessment',
    'ABO and Rhesus determination',
    'Anticoagulation monitoring',
    'Intrinsic pathway screening',
    'Iron storage assessment',
    'Fasting sample preferred',
    'Thalassemia & hemoglobinopathy workup',
    'Strict 8-10 hours fasting',
    'Any time random sample',
    'Exactly 2 hours after meal',
    'Fasting not required, 3-month glycemic control',
    'Urea, Creatinine, eGFR, BUN',
    'Renal filtration marker',
    'Renal clearance marker',
    'Gout & hyperuricemia screening',
    'Bilirubin (T/D), SGPT/ALT, SGOT/AST, ALP',
    'Strict 10-12 hours fasting (Chol, Trig, HDL, LDL)',
    'Fluid and acid-base status',
    'Bone & parathyroid evaluation',
    'Nutritional and hepatic status',
    'Acute pancreatitis evaluation',
    'Acute coronary syndrome evaluation',
    'Myocardial injury marker',
    'Immediate ice transport',
    'Thyroid functional status',
    'Primary thyroid screening',
    'Vitamin D deficiency assessment',
    'Pregnancy & trophoblastic marker',
    'Prostatic screening (avoid pre-exam manipulation)',
    'Acute phase systemic inflammation',
    'Rheumatoid arthritis workup',
    'Specific rheumatoid arthritis marker',
    'Autoimmune & connective tissue disease workup',
    'Systemic lupus erythematosus workup',
    'Clean-catch midstream sample',
    'Prior to initiating antibiotics',
    'Examine for ova, cysts, occult blood',
    'Avoid red meat 48 hrs prior',
    'Aseptic technique, at peak fever',
    'Smear and rapid antigen test',
    'Enteric fever screening',
    'Dengue fever detection',
    'Hepatitis B viral screening',
    'Hepatitis C viral screening',
    'Confidential screening',
    'Resting 12-lead trace',
    'Resting cardiac structure and Doppler assessment',
    'Comfortable walking shoes, hold beta blockers if instructed',
    'Maintain daily activity log, keep unit dry',
    'Wear loose sleeve clothing',
    'Hold short-acting bronchodilators 6 hrs prior',
    'Clean, dry hair, sleep deprived if requested',
    'Strict fasting from midnight, arrange escort',
    'Full bowel preparation as per protocol'
);
");

            // 4. Anti-tamper triggers for InvestigationRequests and InvestigationRequestItems
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_tamper;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_investigation_request_tamper
BEFORE UPDATE ON InvestigationRequests
FOR EACH ROW
BEGIN
    SELECT CASE
        WHEN OLD.Status = 'Cancelled'
        THEN RAISE(ABORT, 'Investigation request is cancelled and cannot be modified.')
    END;

    SELECT CASE
        WHEN OLD.Status = 'Active' AND NEW.Status NOT IN ('Active', 'Cancelled')
        THEN RAISE(ABORT, 'Invalid investigation request status transition.')
    END;

    SELECT CASE
        WHEN OLD.IsSealed = 1 AND NEW.IsSealed != 1
        THEN RAISE(ABORT, 'Sealed investigation request cannot be unsealed.')
    END;

    SELECT CASE
        WHEN (OLD.Status = 'Active' OR OLD.IsSealed = 1) AND (
             NEW.Id != OLD.Id
          OR NEW.RequestNumber != OLD.RequestNumber
          OR NEW.PatientId != OLD.PatientId
          OR NEW.RequestDate != OLD.RequestDate
          OR NEW.ClinicalNotes IS NOT OLD.ClinicalNotes
          OR NEW.Instructions IS NOT OLD.Instructions
          OR NEW.DoctorSnapshotJson IS NOT OLD.DoctorSnapshotJson
          OR NEW.PatientSnapshotJson IS NOT OLD.PatientSnapshotJson
          OR NEW.CreatedAtUtc != OLD.CreatedAtUtc
        )
        THEN RAISE(ABORT, 'Clinical and snapshot investigation request fields are immutable.')
    END;
END;
");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_delete;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_investigation_request_delete
BEFORE DELETE ON InvestigationRequests
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Investigation requests cannot be deleted.');
END;
");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_delete;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_investigation_request_item_delete
BEFORE DELETE ON InvestigationRequestItems
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Investigation request items cannot be deleted.');
END;
");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_update;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_investigation_request_item_update
BEFORE UPDATE ON InvestigationRequestItems
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Investigation request items cannot be updated.');
END;
");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_insert_after_terminal;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_investigation_request_item_insert_after_terminal
BEFORE INSERT ON InvestigationRequestItems
FOR EACH ROW
WHEN (SELECT IsSealed FROM InvestigationRequests WHERE Id = NEW.RequestId) = 1 
  OR (SELECT Status FROM InvestigationRequests WHERE Id = NEW.RequestId) = 'Cancelled'
BEGIN
    SELECT RAISE(ABORT, 'Cannot add items to a sealed or cancelled investigation request.');
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_insert_after_terminal;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_item_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_investigation_request_tamper;");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "InvestigationRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "InvestigationRequests");

            migrationBuilder.DropColumn(
                name: "IsSealed",
                table: "InvestigationRequests");
        }
    }
}

