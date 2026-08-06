using System;
using System.Collections.Generic;
using System.Linq;
using ElevateED.Models;

namespace ElevateED
{
    // Seeds South African career fields, careers with APS / subject requirements,
    // and interest-quiz questions. Idempotent: does nothing once fields exist.
    public static class CareerGuidanceSeeder
    {
        // Lightweight requirement descriptor used while building seed data.
        private class Req
        {
            public string Code;
            public int Level;
            public bool Compulsory = true;

            public Req(string code, int level, bool compulsory = true)
            {
                Code = code;
                Level = level;
                Compulsory = compulsory;
            }
        }

        public static void Seed(ElevateEDContext context)
        {
            if (context.CareerFields.Any()) return;

            // Resolve subject ids by their stable Code.
            var subjectIdByCode = context.Subjects
                .ToList()
                .GroupBy(s => (s.Code ?? "").ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);

            if (!subjectIdByCode.Any()) return; // subjects not seeded yet — skip

            // ---- Career fields ----
            var engineering = new CareerField { Name = "Engineering & Built Environment", IconClass = "cogs", Description = "Design and build machines, structures, and infrastructure." };
            var health = new CareerField { Name = "Health Sciences", IconClass = "heartbeat", Description = "Care for people's physical and mental wellbeing." };
            var commerce = new CareerField { Name = "Commerce & Business", IconClass = "chart-line", Description = "Finance, accounting, economics, and managing organisations." };
            var law = new CareerField { Name = "Law & Justice", IconClass = "balance-scale", Description = "Uphold the law, argue cases, and protect rights." };
            var it = new CareerField { Name = "Information Technology", IconClass = "laptop-code", Description = "Build software, analyse data, and secure systems." };
            var education = new CareerField { Name = "Education", IconClass = "chalkboard-teacher", Description = "Teach and develop learners of all ages." };
            var arts = new CareerField { Name = "Arts, Media & Design", IconClass = "palette", Description = "Create, communicate, and design visually and in words." };
            var science = new CareerField { Name = "Natural & Agricultural Sciences", IconClass = "leaf", Description = "Study living things, the environment, and farming." };

            var fields = new[] { engineering, health, commerce, law, it, education, arts, science };
            context.CareerFields.AddRange(fields);
            context.SaveChanges();

            // Local helper: build a Career with its subject requirements.
            Func<CareerField, string, string, string, string, int, Req[], Career> make =
                (field, name, qualification, whereToStudy, description, minAps, reqs) =>
                {
                    var career = new Career
                    {
                        CareerFieldId = field.Id,
                        Name = name,
                        TypicalQualification = qualification,
                        WhereToStudy = whereToStudy,
                        Description = description,
                        MinimumAps = minAps
                    };
                    foreach (var r in reqs)
                    {
                        if (!subjectIdByCode.ContainsKey(r.Code)) continue;
                        career.SubjectRequirements.Add(new CareerSubjectRequirement
                        {
                            SubjectId = subjectIdByCode[r.Code],
                            MinimumLevel = r.Level,
                            IsCompulsory = r.Compulsory
                        });
                    }
                    return career;
                };

            var careers = new List<Career>
            {
                // Engineering & Built Environment
                make(engineering, "Civil Engineer", "BEng / BSc (Eng) Civil", "UCT, Wits, UKZN, Stellenbosch", "Design roads, bridges, dams, and buildings.", 34,
                    new[] { new Req("MATH", 6), new Req("PHYS", 5), new Req("ENG", 4) }),
                make(engineering, "Mechanical Engineer", "BEng / BSc (Eng) Mechanical", "UKZN, Wits, UP, Stellenbosch", "Design engines, machines, and mechanical systems.", 34,
                    new[] { new Req("MATH", 6), new Req("PHYS", 5), new Req("ENG", 4) }),
                make(engineering, "Electrical Engineer", "BEng / BSc (Eng) Electrical", "UCT, Wits, UKZN, UP", "Work with power, electronics, and control systems.", 35,
                    new[] { new Req("MATH", 6), new Req("PHYS", 6), new Req("ENG", 4) }),
                make(engineering, "Chemical Engineer", "BEng Chemical", "UCT, Wits, UP, Stellenbosch", "Turn raw materials into products at industrial scale.", 36,
                    new[] { new Req("MATH", 6), new Req("PHYS", 6), new Req("ENG", 4) }),
                make(engineering, "Architect", "BAS / BArch", "UCT, Wits, UKZN, UJ", "Design buildings and spaces people live and work in.", 30,
                    new[] { new Req("MATH", 5), new Req("ENG", 4), new Req("CART", 4, false) }),
                make(engineering, "Quantity Surveyor", "BSc Quantity Surveying", "UCT, Wits, UP, NMU", "Manage the cost and budget of construction projects.", 30,
                    new[] { new Req("MATH", 5), new Req("ENG", 4) }),

                // Health Sciences
                make(health, "Medical Doctor", "MBChB", "UCT, Wits, UKZN, UP, SU", "Diagnose and treat illness and injury.", 38,
                    new[] { new Req("MATH", 6), new Req("PHYS", 6), new Req("LIFE", 6), new Req("ENG", 5) }),
                make(health, "Dentist", "BDS / BChD", "Wits, UP, UWC", "Care for teeth, gums, and oral health.", 36,
                    new[] { new Req("MATH", 6), new Req("PHYS", 5), new Req("LIFE", 5), new Req("ENG", 5) }),
                make(health, "Pharmacist", "BPharm", "Rhodes, UWC, UKZN, NWU", "Prepare and dispense medicines and advise patients.", 34,
                    new[] { new Req("MATH", 5), new Req("PHYS", 5), new Req("LIFE", 5), new Req("ENG", 4) }),
                make(health, "Physiotherapist", "BSc Physiotherapy", "UCT, Wits, UKZN, UP", "Help patients recover movement and manage pain.", 32,
                    new[] { new Req("MATH", 4), new Req("PHYS", 4), new Req("LIFE", 5), new Req("ENG", 4) }),
                make(health, "Professional Nurse", "BNurs", "UKZN, UP, UWC, UJ", "Provide frontline care in hospitals and clinics.", 28,
                    new[] { new Req("LIFE", 4), new Req("ENG", 4), new Req("MATH", 3) }),
                make(health, "Dietitian", "BSc Dietetics", "UKZN, UP, NWU", "Advise people on nutrition and healthy eating.", 30,
                    new[] { new Req("LIFE", 5), new Req("MATH", 4), new Req("ENG", 4) }),

                // Commerce & Business
                make(commerce, "Chartered Accountant (CA)", "BCom Accounting (CTA)", "UCT, Wits, UP, SU, UKZN", "Audit, report, and advise on business finances.", 34,
                    new[] { new Req("MATH", 5), new Req("ACCT", 5), new Req("ENG", 4) }),
                make(commerce, "Actuary", "BSc / BCom Actuarial Science", "UCT, Wits, UP, SU", "Model financial risk using advanced maths and statistics.", 40,
                    new[] { new Req("MATH", 7), new Req("ENG", 5) }),
                make(commerce, "Economist", "BCom Economics", "UCT, Wits, UP, Rhodes", "Study markets, policy, and how economies work.", 32,
                    new[] { new Req("MATH", 5), new Req("ECON", 4, false), new Req("ENG", 4) }),
                make(commerce, "Investment / Finance Analyst", "BCom Finance", "UCT, Wits, UP, SU", "Analyse investments and manage money for clients.", 34,
                    new[] { new Req("MATH", 5), new Req("ACCT", 4, false), new Req("ENG", 4) }),
                make(commerce, "Marketing Manager", "BCom Marketing", "UJ, UP, UKZN, NWU", "Plan how products are promoted and sold.", 28,
                    new[] { new Req("BSTD", 4, false), new Req("ENG", 4) }),
                make(commerce, "Human Resources Manager", "BCom Human Resource Management", "UJ, UP, UNISA, NWU", "Recruit, develop, and support an organisation's people.", 26,
                    new[] { new Req("BSTD", 4, false), new Req("ENG", 4) }),

                // Law & Justice
                make(law, "Lawyer / Advocate", "LLB", "UCT, Wits, UP, UKZN, UWC", "Advise clients and argue cases in court.", 33,
                    new[] { new Req("ENG", 5), new Req("HIST", 4, false) }),
                make(law, "Prosecutor", "LLB", "UP, UKZN, UFS, NWU", "Present the state's case against accused persons.", 32,
                    new[] { new Req("ENG", 5), new Req("HIST", 4, false) }),
                make(law, "Paralegal", "Diploma in Paralegal Studies", "UNISA, Damelin, private colleges", "Support lawyers with research and case preparation.", 24,
                    new[] { new Req("ENG", 4) }),
                make(law, "Police Officer / Detective", "SAPS training / Policing Diploma", "SAPS Academy, UNISA, TVET colleges", "Protect communities and investigate crime.", 22,
                    new[] { new Req("ENG", 3) }),

                // Information Technology
                make(it, "Software Developer", "BSc Computer Science", "UCT, Wits, UP, SU, UKZN", "Design and build applications and systems.", 32,
                    new[] { new Req("MATH", 5), new Req("ENG", 4), new Req("IT", 5, false) }),
                make(it, "Data Scientist", "BSc Data Science / Statistics", "UCT, Wits, SU, UP", "Find insights and build models from large datasets.", 36,
                    new[] { new Req("MATH", 6), new Req("ENG", 4) }),
                make(it, "Cybersecurity Analyst", "BSc IT / Information Security", "UP, UJ, NWU, UNISA", "Protect systems and data from cyber attacks.", 32,
                    new[] { new Req("MATH", 5), new Req("ENG", 4), new Req("IT", 4, false) }),
                make(it, "Network / Systems Administrator", "Diploma / BSc IT", "UJ, TUT, CPUT, UNISA", "Keep networks and servers running reliably.", 28,
                    new[] { new Req("MATH", 4), new Req("IT", 4, false) }),

                // Education
                make(education, "Foundation Phase Teacher", "BEd Foundation Phase", "UP, UKZN, UNISA, NWU", "Teach the youngest learners (Grade R-3).", 26,
                    new[] { new Req("ENG", 4) }),
                make(education, "High School Teacher", "BEd Senior & FET Phase", "UP, Wits, UKZN, UJ, UNISA", "Teach subjects to Grade 8-12 learners.", 28,
                    new[] { new Req("ENG", 4), new Req("MATH", 3) }),
                make(education, "Educational Psychologist", "BEd + MEd Educational Psychology", "UP, Wits, UKZN, SU", "Support learners with emotional and learning needs.", 34,
                    new[] { new Req("ENG", 5), new Req("LIFE", 4, false) }),

                // Arts, Media & Design
                make(arts, "Graphic Designer", "BA / Diploma Graphic Design", "UJ, TUT, CPUT, Vega", "Design logos, layouts, and visual communication.", 26,
                    new[] { new Req("CART", 4, false), new Req("ENG", 4) }),
                make(arts, "Journalist", "BA Journalism / Media Studies", "Rhodes, Wits, UJ, Stellenbosch", "Research and report news across media.", 28,
                    new[] { new Req("ENG", 5), new Req("HIST", 4, false) }),
                make(arts, "Fine / Visual Artist", "BA Fine Arts", "Wits, UCT (Michaelis), UJ, Rhodes", "Create visual art across media.", 24,
                    new[] { new Req("CART", 4, false), new Req("ENG", 3) }),

                // Natural & Agricultural Sciences
                make(science, "Veterinarian", "BVSc", "University of Pretoria (Onderstepoort)", "Diagnose and treat animals.", 38,
                    new[] { new Req("MATH", 6), new Req("PHYS", 5), new Req("LIFE", 6), new Req("ENG", 5) }),
                make(science, "Environmental Scientist", "BSc Environmental Science", "UCT, Rhodes, UKZN, NWU", "Study and protect ecosystems and the environment.", 30,
                    new[] { new Req("LIFE", 5), new Req("GEOG", 4, false), new Req("MATH", 4) }),
                make(science, "Agricultural Scientist", "BSc Agriculture", "UP, SU, UKZN, UFS", "Improve crops, livestock, and food production.", 30,
                    new[] { new Req("MATH", 4), new Req("LIFE", 5), new Req("AGRI", 4, false) }),
                make(science, "Biotechnologist", "BSc Biotechnology", "UWC, UP, Wits, NWU", "Use living systems to develop products and medicines.", 32,
                    new[] { new Req("LIFE", 5), new Req("PHYS", 4), new Req("MATH", 4) }),
            };

            context.Careers.AddRange(careers);
            context.SaveChanges();

            // ---- Interest quiz questions (Likert; a strong agree points to the field) ----
            var questions = new List<InterestQuestion>
            {
                new InterestQuestion { CareerFieldId = engineering.Id, Text = "I enjoy taking things apart to understand how they work." },
                new InterestQuestion { CareerFieldId = engineering.Id, Text = "I like solving practical problems using maths and physics." },
                new InterestQuestion { CareerFieldId = engineering.Id, Text = "I would enjoy designing or building machines and structures." },

                new InterestQuestion { CareerFieldId = health.Id, Text = "I want to help people recover from illness or injury." },
                new InterestQuestion { CareerFieldId = health.Id, Text = "I am fascinated by how the human body works." },
                new InterestQuestion { CareerFieldId = health.Id, Text = "I stay calm and focused in stressful or emergency situations." },

                new InterestQuestion { CareerFieldId = commerce.Id, Text = "I enjoy working with numbers, money, and budgets." },
                new InterestQuestion { CareerFieldId = commerce.Id, Text = "I like the idea of running or managing a business." },
                new InterestQuestion { CareerFieldId = commerce.Id, Text = "I follow news about the economy, business, or markets." },

                new InterestQuestion { CareerFieldId = law.Id, Text = "I enjoy debating and arguing a point convincingly." },
                new InterestQuestion { CareerFieldId = law.Id, Text = "I care strongly about justice, rules, and fairness." },

                new InterestQuestion { CareerFieldId = it.Id, Text = "I enjoy working with computers and technology." },
                new InterestQuestion { CareerFieldId = it.Id, Text = "I would like to build apps, games, or write code." },
                new InterestQuestion { CareerFieldId = it.Id, Text = "I like figuring out why a device or program isn't working." },

                new InterestQuestion { CareerFieldId = education.Id, Text = "I enjoy explaining things and helping others learn." },
                new InterestQuestion { CareerFieldId = education.Id, Text = "I would find it rewarding to work with children or teenagers." },

                new InterestQuestion { CareerFieldId = arts.Id, Text = "I express myself through art, design, or writing." },
                new InterestQuestion { CareerFieldId = arts.Id, Text = "I have a strong sense of creativity and imagination." },

                new InterestQuestion { CareerFieldId = science.Id, Text = "I am curious about nature, plants, animals, and the environment." },
                new InterestQuestion { CareerFieldId = science.Id, Text = "I enjoy doing experiments or fieldwork outdoors." },
            };

            context.InterestQuestions.AddRange(questions);
            context.SaveChanges();
        }
    }
}
