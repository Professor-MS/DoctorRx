using System;
using System.Collections.Generic;

namespace DoctorRx.Application.Common;

/// <summary>
/// Reference formulary knowledge base mapping common active substances and brand formulations
/// to their therapeutic classifications and primary clinical disease indications.
/// </summary>
public static class MedicineClinicalKnowledge
{
    private record ClinicalEntry(string TherapeuticClass, string PrimaryIndications);

    private static readonly Dictionary<string, ClinicalEntry> ClinicalCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        // Analgesics & Antipyretics
        ["Paracetamol"] = new("Analgesic & Antipyretic", "Pain and fever relief (headaches, muscular aches, feverish colds)"),
        ["Panadol"] = new("Analgesic & Antipyretic", "Pain and fever relief (headaches, body aches, dental pain)"),
        ["Calpol"] = new("Analgesic & Antipyretic", "Fever and mild-to-moderate pain in infants and children"),
        ["Disprin"] = new("Analgesic & Antiplatelet", "Headache, toothache, fever & cardiovascular antiplatelet protection"),

        // NSAIDs
        ["Ibuprofen"] = new("NSAID", "Pain, inflammation, osteoarthritis, rheumatoid arthritis & fever"),
        ["Brufen"] = new("NSAID", "Pain, inflammation, joint stiffness, sprains & fever"),
        ["Diclofenac sodium"] = new("NSAID", "Acute musculoskeletal pain, arthritis, sprains & post-traumatic inflammation"),
        ["Diclofenac"] = new("NSAID", "Inflammatory pain, rheumatoid arthritis & osteoarthritis"),
        ["Naproxen"] = new("NSAID", "Long-acting relief for joint stiffness, arthritis & acute inflammatory pain"),
        ["Mefenamic Acid"] = new("NSAID", "Primary dysmenorrhea (menstrual cramps), dental pain & acute pain relief"),
        ["Ponstan"] = new("NSAID", "Primary dysmenorrhea (menstrual pain), toothaches & surgical pain"),
        ["Celecoxib"] = new("COX-2 Inhibitor NSAID", "Osteoarthritis, rheumatoid arthritis & acute pain with lower GI risk"),

        // Cold & Cough Combinations
        ["Arinac Forte"] = new("NSAID & Decongestant", "Cold, flu, nasal congestion, fever & sinus pain relief"),
        ["Arinac"] = new("NSAID & Decongestant", "Relief of cold, flu symptoms, sinus pressure & nasal congestion"),
        ["Hydryllin"] = new("Bronchodilator & Expectorant", "Bronchial asthma cough, asthmatic bronchitis & chest tightness"),

        // Antihistamines & Allergy
        ["Cetirizine"] = new("Antihistamine", "Allergic rhinitis (hay fever), sneezing, runny nose & chronic urticaria (hives)"),
        ["Zyrtec"] = new("Antihistamine", "Allergy symptoms, hay fever & itchy skin conditions"),
        ["Loratadine"] = new("Non-drowsy Antihistamine", "Allergic rhinitis, allergic conjunctivitis & urticaria symptom relief"),
        ["Clarityn"] = new("Non-drowsy Antihistamine", "Seasonal allergic rhinitis, sneezing, nasal itching & hives"),
        ["Fexofenadine"] = new("Non-sedating Antihistamine", "Seasonal allergic rhinitis (hay fever) & chronic idiopathic urticaria"),
        ["Telfast"] = new("Non-sedating Antihistamine", "Hay fever, skin allergies & chronic hives relief"),
        ["Chlorpheniramine"] = new("First-generation Antihistamine", "Allergy symptom relief, itching, runny nose & allergic skin reactions"),
        ["Piriton"] = new("Antihistamine", "Emergency and acute allergic reaction symptom control & itching relief"),

        // Respiratory & Asthma
        ["Montelukast"] = new("Leukotriene Receptor Antagonist", "Persistent asthma maintenance & seasonal allergic rhinitis"),
        ["Salbutamol"] = new("Short-acting Bronchodilator", "Acute asthma attacks, bronchospasm, wheezing & exercise-induced asthma"),
        ["Ventolin"] = new("Short-acting Bronchodilator", "Relief of bronchospasm in bronchial asthma, bronchitis & COPD"),
        ["Budesonide"] = new("Inhaled Corticosteroid", "Maintenance control of chronic bronchial asthma & airway inflammation"),
        ["Pulmicort"] = new("Inhaled Corticosteroid", "Prophylactic management of persistent asthma in adults and children"),
        ["Ipratropium bromide"] = new("Anticholinergic Bronchodilator", "COPD bronchospasm & acute asthma exacerbation adjunct"),
        ["Atrovent"] = new("Anticholinergic Bronchodilator", "Chronic obstructive pulmonary disease & persistent bronchospasm"),

        // Gastrointestinal: Acid Suppression & Ulcers
        ["Omeprazole"] = new("Proton Pump Inhibitor", "Acid reflux (GERD), heartburn, gastric & duodenal peptic ulcers"),
        ["Risek"] = new("Proton Pump Inhibitor", "Heartburn, acid reflux (GERD) & peptic ulcer healing"),
        ["Losec"] = new("Proton Pump Inhibitor", "Gastroesophageal reflux, erosive esophagitis & peptic ulcer disease"),
        ["Esomeprazole"] = new("Proton Pump Inhibitor", "Gastroesophageal reflux disease (GERD), erosive esophagitis & peptic ulcers"),
        ["Nexium"] = new("Proton Pump Inhibitor", "Erosive esophagitis healing, acid reflux & NSAID-associated ulcer prevention"),
        ["Pantoprazole"] = new("Proton Pump Inhibitor", "Acid-related disorders, erosive esophagitis & Zollinger-Ellison syndrome"),
        ["Famotidine"] = new("H2 Receptor Antagonist", "Heartburn, acid indigestion & gastric hypersecretion relief"),

        // Gastrointestinal: Antiemetics, Motility & Rehydration
        ["Ondansetron"] = new("Antiemetic (5-HT3 Antagonist)", "Chemotherapy, radiation & postoperative nausea and vomiting prevention"),
        ["Oral rehydration salts"] = new("Electrolyte Solution", "Prevention and treatment of dehydration caused by acute diarrhea"),
        ["Loperamide"] = new("Antidiarrheal", "Symptomatic control of acute non-infectious diarrhea & traveler's diarrhea"),
        ["Imodium"] = new("Antidiarrheal", "Acute diarrhea symptom control and reduction of bowel movements"),
        ["Lactulose"] = new("Osmotic Laxative", "Chronic constipation relief & portal-systemic encephalopathy management"),

        // Antidiabetic Medicines
        ["Metformin"] = new("Biguanide Antidiabetic", "Type 2 diabetes first-line glycemic control & insulin resistance"),
        ["Glucophage"] = new("Biguanide Antidiabetic", "Type 2 diabetes blood glucose regulation & insulin resistance"),
        ["Gliclazide"] = new("Sulfonylurea Antidiabetic", "Type 2 diabetes glycemic control in non-obese or elderly patients"),
        ["Diamicron"] = new("Sulfonylurea Antidiabetic", "Type 2 diabetes blood sugar reduction when diet alone is insufficient"),
        ["Glimepiride"] = new("Sulfonylurea Antidiabetic", "Type 2 diabetes blood sugar reduction in combination with diet and exercise"),
        ["Amaryl"] = new("Sulfonylurea Antidiabetic", "Type 2 diabetes glycemic control to lower blood glucose levels"),
        ["Sitagliptin"] = new("DPP-4 Inhibitor", "Type 2 diabetes blood glucose regulation with low risk of hypoglycemia"),
        ["Januvia"] = new("DPP-4 Inhibitor", "Glycemic control improvement in patients with type 2 diabetes"),
        ["Empagliflozin"] = new("SGLT2 Inhibitor", "Type 2 diabetes blood sugar control, heart failure & kidney protection"),
        ["Jardiance"] = new("SGLT2 Inhibitor", "Type 2 diabetes glycemic control and cardiovascular mortality risk reduction"),

        // Cardiovascular: Antihypertensives & Heart Failure
        ["Amlodipine"] = new("Calcium Channel Blocker", "Hypertension (high blood pressure) & chronic stable angina"),
        ["Norvasc"] = new("Calcium Channel Blocker", "Essential hypertension & coronary artery disease (angina) management"),
        ["Losartan"] = new("Angiotensin II Receptor Blocker", "Hypertension & reduction of renal disease progression in diabetic patients"),
        ["Cozaar"] = new("Angiotensin II Receptor Blocker", "High blood pressure control & cardiovascular stroke risk reduction"),
        ["Enalapril"] = new("ACE Inhibitor", "Hypertension, chronic heart failure & asymptomatic left ventricular dysfunction"),
        ["Bisoprolol"] = new("Cardioselective Beta Blocker", "Hypertension, angina pectoris & stable chronic heart failure"),
        ["Concor"] = new("Cardioselective Beta Blocker", "High blood pressure control & ischemic heart disease management"),
        ["Hydrochlorothiazide"] = new("Thiazide Diuretic", "Essential hypertension & edema associated with congestive heart failure"),
        ["Furosemide"] = new("Loop Diuretic", "Edema in heart failure, acute pulmonary edema & fluid overload"),
        ["Lasix"] = new("Loop Diuretic", "Fluid retention, congestive heart failure & renal edema relief"),

        // Cardiovascular: Statins & Antiplatelets
        ["Atorvastatin"] = new("Statin (Lipid-lowering)", "High cholesterol, hyperlipidemia & prevention of cardiovascular events"),
        ["Lipitor"] = new("Statin (Lipid-lowering)", "Elevated LDL cholesterol reduction & atherosclerotic cardiovascular disease prevention"),
        ["Rosuvastatin"] = new("Statin (Lipid-lowering)", "Severe hypercholesterolemia, high triglycerides & cardiovascular risk reduction"),
        ["Crestor"] = new("Statin (Lipid-lowering)", "Primary hyperlipidemia treatment & slowing atherosclerosis progression"),
        ["Clopidogrel"] = new("Antiplatelet Agent", "Prevention of stroke, myocardial infarction & blood clots in arterial disease"),
        ["Plavix"] = new("Antiplatelet Agent", "Atherothrombotic event prevention in patients with acute coronary syndrome"),

        // Antibiotics
        ["Amoxicillin"] = new("Penicillin Antibiotic", "Bacterial chest, ear, nose, throat, dental & urinary tract infections"),
        ["Amoxil"] = new("Penicillin Antibiotic", "Bacterial respiratory, ENT & uncomplicated genitourinary infections"),
        ["Amoxicillin + clavulanic acid"] = new("Broad-spectrum Antibiotic", "Resistant respiratory, skin, soft tissue & dental bacterial infections"),
        ["Augmentin"] = new("Broad-spectrum Antibiotic", "Severe respiratory, ENT, skin, urinary tract & soft tissue bacterial infections"),
        ["Azithromycin"] = new("Macrolide Antibiotic", "Community-acquired pneumonia, acute bronchitis, sinusitis & throat infections"),
        ["Zithromax"] = new("Macrolide Antibiotic", "Bacterial respiratory tract infections, skin infections & ear infections"),
        ["Clarithromycin"] = new("Macrolide Antibiotic", "H. pylori stomach infection eradication, pneumonia & skin bacterial infections"),
        ["Klaricid"] = new("Macrolide Antibiotic", "Upper and lower respiratory tract infections & peptic ulcer H. pylori regimens"),
        ["Cefixime"] = new("Cephalosporin Antibiotic", "Typhoid fever, bacterial bronchitis, otitis media & uncomplicated UTIs"),
        ["Cefspan"] = new("Cephalosporin Antibiotic", "Bacterial upper respiratory, ENT & urinary tract infections"),
        ["Cephalexin"] = new("Cephalosporin Antibiotic", "Skin, soft tissue, bone, dental & respiratory bacterial infections"),
        ["Keflex"] = new("Cephalosporin Antibiotic", "Bacterial infections of the skin, bone, respiratory tract & urinary tract"),
        ["Doxycycline"] = new("Tetracycline Antibiotic", "Atypical pneumonia, severe acne vulgaris, pelvic infections & malaria prevention"),
        ["Vibramycin"] = new("Tetracycline Antibiotic", "Bacterial respiratory infections, severe acne, chlamydia & rickettsial diseases"),
        ["Metronidazole"] = new("Antimicrobial & Antiprotozoal", "Anaerobic bacterial infections, dental abscesses, amebiasis & giardiasis"),
        ["Flagyl"] = new("Antimicrobial & Antiprotozoal", "Intestinal amoebiasis, giardiasis, trichomoniasis & anaerobic bacterial infections"),
        ["Ciprofloxacin"] = new("Fluoroquinolone Antibiotic", "Complicated urinary tract infections, severe diarrhea & bone/joint infections"),
        ["Ciproxin"] = new("Fluoroquinolone Antibiotic", "Severe bacterial urinary tract, gastrointestinal & intra-abdominal infections"),
        ["Nitrofurantoin"] = new("Urinary Antibacterial", "Acute uncomplicated lower urinary tract infections (cystitis) & UTI prophylaxis"),

        // Antifungals & Antivirals
        ["Fluconazole"] = new("Antifungal", "Vaginal yeast infections (candidiasis), oral thrush & systemic fungal infections"),
        ["Diflucan"] = new("Antifungal", "Mucosal candidiasis, vaginal thrush & systemic fungal disease prevention"),
        ["Clotrimazole"] = new("Topical Antifungal", "Athlete's foot, jock itch, ringworm & fungal skin candidiasis"),
        ["Canesten"] = new("Topical Antifungal", "Fungal skin infections, candidal intertrigo & tinea pedis"),
        ["Terbinafine"] = new("Topical Antifungal", "Tinea pedis (athlete's foot), tinea cruris & stubborn fungal skin infections"),
        ["Lamisil"] = new("Topical Antifungal", "Fungal skin infections, ringworm & athlete's foot"),
        ["Acyclovir"] = new("Antiviral", "Herpes simplex, genital herpes, shingles (herpes zoster) & chickenpox"),
        ["Zovirax"] = new("Antiviral", "Cold sores, genital herpes virus flare-ups & shingles treatment"),

        // Dermatology & Topical Agents
        ["Mupirocin"] = new("Topical Antibiotic", "Impetigo, folliculitis & superficial bacterial skin infections"),
        ["Bactroban"] = new("Topical Antibiotic", "Bacterial impetigo, infected eczema & minor traumatic skin wounds"),
        ["Hydrocortisone"] = new("Mild Topical Corticosteroid", "Mild eczema, contact dermatitis, insect stings & skin redness/itching"),
        ["Betamethasone valerate"] = new("Potent Topical Corticosteroid", "Severe eczema, psoriasis, neurodermatitis & lichen planus"),
        ["Betnovate"] = new("Potent Topical Corticosteroid", "Inflammatory eczema, stubborn dermatitis & psoriasis flare-ups"),
        ["Permethrin"] = new("Scabicide & Pediculicide", "Scabies infestation & crab lice eradication in adults and children"),
        ["Loxazol"] = new("Scabicide", "Eradication of human scabies mites and associated nocturnal itching"),
        ["Benzoyl peroxide"] = new("Topical Acne Treatment", "Acne vulgaris, inflammatory pimples, blackheads & blemishes"),
        ["Ketoconazole"] = new("Antifungal", "Seborrheic dermatitis, stubborn dandruff & fungal skin infections"),
        ["Nizoral"] = new("Antifungal Shampoo", "Dandruff, seborrheic dermatitis & pityriasis versicolor on scalp"),
        ["Zinc oxide"] = new("Topical Skin Protectant", "Diaper rash, minor skin burns, chafing & soothing barrier protection"),
        ["SudoCream"] = new("Soothing Antiseptic Cream", "Diaper rash, eczema, minor abrasions & bedsores protection"),

        // Ophthalmology & Otic
        ["Timolol"] = new("Ophthalmic Beta Blocker", "Chronic open-angle glaucoma & lowering elevated intraocular pressure"),
        ["Latanoprost"] = new("Ophthalmic Prostaglandin Analogue", "Open-angle glaucoma & ocular hypertension intraocular pressure reduction"),
        ["Xalatan"] = new("Ophthalmic Prostaglandin Analogue", "Elevated intraocular pressure in open-angle glaucoma patients"),
        ["Chloramphenicol"] = new("Ophthalmic Antibiotic", "Acute bacterial conjunctivitis & superficial bacterial eye infections"),
        ["Carbamide peroxide"] = new("Cerumenolytic Ear Drops", "Impacted earwax softening, loosening and gentle removal"),
        ["Ofloxacin"] = new("Otic Antibiotic", "Bacterial otitis externa, ear canal infections & chronic otitis media"),

        // Endocrinology, Gout & Urology
        ["Levothyroxine"] = new("Thyroid Hormone", "Hypothyroidism (underactive thyroid) hormone replacement therapy"),
        ["Thyroxine"] = new("Thyroid Hormone", "Underactive thyroid gland substitution therapy & goiter suppression"),
        ["Prednisolone"] = new("Systemic Corticosteroid", "Severe asthma flares, allergic conditions & autoimmune inflammation"),
        ["Allopurinol"] = new("Xanthine Oxidase Inhibitor", "Chronic gout prevention, hyperuricemia & urate kidney stones"),
        ["Zyloric"] = new("Xanthine Oxidase Inhibitor", "Gouty arthritis prevention & hyperuricemia in adults"),
        ["Colchicine"] = new("Antigout Medicine", "Acute gout flares pain relief and familial Mediterranean fever"),
        ["Tamsulosin"] = new("Alpha-1A Blocker", "Benign prostatic hyperplasia (BPH) urinary obstruction & hesitancy relief"),
        ["Omnic"] = new("Alpha-1A Blocker", "Lower urinary tract symptoms associated with enlarged prostate (BPH)"),
        ["Finasteride"] = new("5-Alpha Reductase Inhibitor", "Benign prostatic hyperplasia (BPH) prostate enlargement reduction"),
        ["Proscar"] = new("5-Alpha Reductase Inhibitor", "Benign prostatic hyperplasia symptoms and urinary retention risk reduction"),

        // Mental Health & Neurology
        ["Sertraline"] = new("SSRI Antidepressant", "Major depressive disorder, panic disorder, obsessive-compulsive disorder & PTSD"),
        ["Zoloft"] = new("SSRI Antidepressant", "Depression, generalized anxiety disorder, panic attacks & social phobia"),
        ["Fluoxetine"] = new("SSRI Antidepressant", "Major depressive disorder, bulimia nervosa & obsessive-compulsive disorder"),
        ["Prozac"] = new("SSRI Antidepressant", "Clinical depression, obsessive-compulsive disorder & panic episodes"),
        ["Amitriptyline"] = new("Tricyclic Antidepressant", "Depression, chronic neuropathic nerve pain & migraine prophylaxis"),
        ["Diazepam"] = new("Benzodiazepine", "Short-term acute anxiety, muscle spasm relief & acute seizure management"),
        ["Valium"] = new("Benzodiazepine", "Severe acute anxiety, agitation & skeletal muscle spasms"),
        ["Levetiracetam"] = new("Antiepileptic", "Partial-onset seizures, myoclonic seizures & generalized tonic-clonic epilepsy"),
        ["Keppra"] = new("Antiepileptic", "Seizure control in epilepsy patients as monotherapy or adjunctive therapy"),
        ["Sodium valproate"] = new("Antiepileptic & Mood Stabilizer", "Generalized and partial epilepsy seizures & bipolar disorder mania"),
        ["Epival"] = new("Antiepileptic & Mood Stabilizer", "Epileptic seizures control & bipolar affective disorder prophylaxis"),
        ["Carbamazepine"] = new("Antiepileptic", "Epileptic seizures & severe trigeminal facial nerve neuralgia"),
        ["Tegretol"] = new("Antiepileptic", "Partial and secondary generalized seizures & trigeminal neuralgia"),
        ["Gabapentin"] = new("Neuropathic Pain Analgesic", "Diabetic neuropathy, postherpetic nerve pain & focal seizures"),
        ["Neurontin"] = new("Neuropathic Pain Analgesic", "Peripheral neuropathic pain relief & adjunctive seizure therapy"),
        ["Pregabalin"] = new("Neuropathic Pain Analgesic", "Diabetic neuropathic pain, postherpetic neuralgia & fibromyalgia"),
        ["Lyrica"] = new("Neuropathic Pain Analgesic", "Neuropathic pain, fibromyalgia & generalized anxiety disorder"),

        // Supplements & Minerals
        ["Ferrous sulfate"] = new("Iron Supplement", "Iron deficiency anemia treatment and replenishment of iron stores"),
        ["Fefol"] = new("Iron & Folic Acid", "Iron and folate deficiency treatment, especially during pregnancy"),
        ["Folic acid"] = new("Essential B-Vitamin", "Folate deficiency anemia & neural tube defect prevention in pregnancy"),
        ["Cholecalciferol (vitamin D3)"] = new("Vitamin D3 Supplement", "Vitamin D deficiency treatment, bone mineral health & osteoporosis support"),
        ["Sunny D"] = new("Vitamin D3 Supplement", "Correction of vitamin D deficiency and healthy bone maintenance"),
        ["Zinc sulfate"] = new("Trace Mineral Supplement", "Adjunct treatment in pediatric acute diarrhea & zinc deficiency"),
        ["Zincat"] = new("Zinc Supplement", "Diarrhea recovery acceleration in children & trace element supplementation"),

        // Emergency & Anesthetics
        ["Adrenaline (epinephrine)"] = new("Emergency Vasopressor", "Life-threatening acute anaphylactic shock & severe allergic emergencies"),
        ["Adrenaline"] = new("Emergency Vasopressor", "Emergency treatment of severe allergic anaphylaxis & cardiac arrest"),
        ["Lidocaine (lignocaine)"] = new("Local Anesthetic", "Local infiltration anesthesia for minor surgical & dental procedures"),
        ["Xylocaine"] = new("Local Anesthetic", "Local and regional nerve block anesthesia for procedural pain control")
    };

    /// <summary>
    /// Looks up the primary clinical disease indication for a medicine by trade name or active generic substance.
    /// </summary>
    public static string? GetClinicalIndication(string? name, string? genericName)
    {
        if (!string.IsNullOrWhiteSpace(name) && ClinicalCatalog.TryGetValue(name.Trim(), out var entry1))
        {
            return entry1.PrimaryIndications;
        }

        if (!string.IsNullOrWhiteSpace(genericName) && ClinicalCatalog.TryGetValue(genericName.Trim(), out var entry2))
        {
            return entry2.PrimaryIndications;
        }

        // Fuzzy search by word prefix
        if (!string.IsNullOrWhiteSpace(genericName))
        {
            var g = genericName.Trim();
            foreach (var kvp in ClinicalCatalog)
            {
                if (g.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains(g, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value.PrimaryIndications;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();
            foreach (var kvp in ClinicalCatalog)
            {
                if (n.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains(n, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value.PrimaryIndications;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Looks up the therapeutic classification / drug category for a medicine.
    /// </summary>
    public static string? GetTherapeuticCategory(string? name, string? genericName)
    {
        if (!string.IsNullOrWhiteSpace(name) && ClinicalCatalog.TryGetValue(name.Trim(), out var entry1))
        {
            return entry1.TherapeuticClass;
        }

        if (!string.IsNullOrWhiteSpace(genericName) && ClinicalCatalog.TryGetValue(genericName.Trim(), out var entry2))
        {
            return entry2.TherapeuticClass;
        }

        // Fuzzy match
        if (!string.IsNullOrWhiteSpace(genericName))
        {
            var g = genericName.Trim();
            foreach (var kvp in ClinicalCatalog)
            {
                if (g.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains(g, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value.TherapeuticClass;
                }
            }
        }

        return null;
    }
}
