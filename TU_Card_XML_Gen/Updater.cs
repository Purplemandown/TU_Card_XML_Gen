using System.Xml.Linq;
using System.Xml.XPath;
using TU_Card_XML_Gen;

namespace TUComparatorLibrary
{
    public class Updater
    {

        string version = "1.3.2";
        public static List<XElement> skillData;
        public static List<XElement> factionData;
        public static List<XElement> fusionData;
        internal List<XDocument> oldCardXmls = new List<XDocument>();
        internal List<Card> oldCardObjects = new List<Card>();
        internal Dictionary<int, XElement> newXmls = new Dictionary<int, XElement>();
        internal Dictionary<int, int> fileForCardId = new Dictionary<int, int>();

        string oldXmlDirectory;

        private static List<string> keywordOrder = new List<string>()
        {
            "play",
            "attacked",
            "death"
        };

        private static List<string> skillKeywordOrder = new List<string>()
        {
            "play", //trigger
            "evade",
            "payback",
            "revenge",
            "tribute",
            "absorb",
            "barrier",
            "stasis",
            "armored",
            "wall",
            "allegiance",
            "avenge",
            "scavenge",
            "subdue",
            "attacked", //trigger
            "counter",
            "corrosive",
            "poison",
            "inhibit",
            "sabotage",
            "disease",
            "valor",
            "bravery",
            "summon",
            "evolve",
            "enhance",
            "overload",
            "rush",
            "mend",
            "fortify",
            "heal",
            "protect",
            "entrap",
            "rally",
            "enrage",
            "enfeeble",
            "strike",
            "jam",
            "weaken",
            "sunder",
            "siege",
            "besiege",
            "mimic",
            "legion",
            "coalition",
            "venom",
            "pierce",
            "hunt",
            "swipe",
            "drain",
            "leech",
            "berserk",
            "mark",
            "flurry",
            "refresh",
            "death" //trigger
        };

        private static Dictionary<string, string> standardizationDictionary = new Dictionary<string, string>()
        {
            { " xn", " Xeno" },
            { " imp ", " Imperial " },
            { " rd", " Raider" },
            { " bt", " Bloodthirsty" },
            { " rt", " Righteous" },
            { " prog", " Progenitor" },
            { " OnPlay", " play" },
            { " On Play:", " play" },
            { " On Play", " play" },
            { " OnAttacked", " attacked" },
            { " On Attacked:", " attacked" },
            { " On Attacked", " attacked" },
            { " OnDeath", " death" },
            { " On Death:", " death" },
            { " On Death", " death" },
            { " zerk", " berserk" }
        };

        private static List<string> wordsToIgnoreStandardization = new List<string>()
        {
            "imperial"
        };

        private string outputDirectory = $"output-{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}";

        private ConfigStore config;

        public void Run(string oldXmlDirectory, string updateFilePath, ConfigStore config)
        {
            Console.WriteLine($"Running TU_Card_XML_Gen version {version}");
            Console.WriteLine($"Operation: {config.Operation}");

            this.oldXmlDirectory = oldXmlDirectory;

            Initialize(config);

            switch (config.Operation)
            {
                case ConfigStore.UpdaterOperation.Standard:
                    RunStandardUpdate(updateFilePath, config);
                    break;
                case ConfigStore.UpdaterOperation.MassNerfPoison:
                    RunMassNerfPoisonUpdate(config);
                    break;
                default:
                    throw new NotImplementedException("Config indicates an operation that does not exist.  Check your spelling.");
            }
        }

        private void RunMassNerfPoisonUpdate(ConfigStore config)
        {
            string skillUnderTest = "poison";

            List<string> cardsFailedToUpdate = new List<string>();

            // loads into oldCardObjects
            LoadOldCards(oldXmlDirectory);

            //modify the card objects to have the values we want.

            //Find all cards with poison
            // TODO: This query is a little wonky, verify it.
            List<Card> cardsWithPoison = oldCardObjects.Where(x => x.upgradeLevels.Last().Value.skillList.Where(y => y.id.Equals(skillUnderTest)).Count() > 0).ToList();

            //find the associated XMLs and update them.
            List<XElement> cardXMLsToUpdate = new List<XElement>();

            foreach (Card cardToUpdate in cardsWithPoison)
            {
                XElement cardXmlToUpdate = oldCardXmls[cardToUpdate.fileIndex - 1].XPathSelectElement($@"//unit[id='{cardToUpdate.id}']");

                cardXMLsToUpdate.Add(cardXmlToUpdate);
            }

            Console.WriteLine($@"Found {cardXMLsToUpdate.Count} cards with {skillUnderTest} to update.");

            // TODO: You really need to write a method that takes an XML and a Card and updates the former with values from the latter, but that sounds like more work than I want to do right now.
            // Still, noting it for when I do the real mass-nerfer

            for (int i = 0; i < cardXMLsToUpdate.Count; i++)
            {
                string name = "unknown";

                try
                {
                    bool cardWasChanged = false;

                    // get the card (can't foreach because we need to key on the index)
                    XElement cardXmlToUpdate = cardXMLsToUpdate[i];
                    name = cardXmlToUpdate.XPathSelectElement("name").Value;
                    Console.WriteLine($@"Updating {name}...");

                    // for both the base card, and each upgrade level, look for existing poison.
                    // For each that is found, multiply it by the scaling factor, round up, then set the value, as well as a flag that indicates it was changed.
                    XElement basePoison = cardXmlToUpdate.XPathSelectElement($@"skill[@id='{skillUnderTest}']");
                    if (basePoison != null)
                    {
                        decimal newValue = decimal.Parse(basePoison.Attribute("x").Value) * config.MassNerfFactor;

                        int roundedValue = (int)Math.Ceiling(newValue);

                        cardXmlToUpdate.XPathSelectElement($@"skill[@id='{skillUnderTest}']").SetAttributeValue("x", roundedValue);
                        cardWasChanged = true;
                    }

                    List<XElement> upgrades = cardXmlToUpdate.XPathSelectElements("upgrade").ToList();
                    cardXmlToUpdate.XPathSelectElements("upgrade").Remove();

                    foreach (XElement upgrade in upgrades)
                    {
                        XElement upgradePoison = upgrade.XPathSelectElement($@"skill[@id='{skillUnderTest}']");
                        if (upgradePoison != null)
                        {
                            decimal newValue = decimal.Parse(upgradePoison.Attribute("x").Value) * config.MassNerfFactor;

                            int roundedValue = (int)Math.Ceiling(newValue);

                            upgrade.XPathSelectElement($@"skill[@id='{skillUnderTest}']").SetAttributeValue("x", roundedValue);
                            cardWasChanged = true;
                        }

                        cardXmlToUpdate.Add(upgrade);
                    }

                    if (cardWasChanged)
                    {
                        newXmls.Add(int.Parse(cardXmlToUpdate.XPathSelectElement("id").Value), cardXmlToUpdate);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    //Console.WriteLine(ex.StackTrace);
                    cardsFailedToUpdate.Add(name);
                }
            }

            Console.WriteLine($@"{newXmls.Count} cards received updates.  Saving...");


            // Add the file for card ID to the end?
            foreach (Card cardToUpdate in cardsWithPoison)
            {
                // find the root card xml
                fileForCardId.Add(cardToUpdate.id, cardToUpdate.fileIndex);
            }

            SaveCards(cardsFailedToUpdate, newXmls, fileForCardId);
        }

        private void RunStandardUpdate(string updateFilePath, ConfigStore config)
        {
            List<string> cardsFailedToUpdate = new List<string>();

            LoadOldCards(oldXmlDirectory);

            // Load the update file.

            string updateFileContents = File.ReadAllText(updateFilePath).Trim();

            // split the file into cards to find.
            string[] updateCards = updateFileContents.Split($@"{Environment.NewLine}{Environment.NewLine}");


            foreach (string updateCard in updateCards)
            {
                // the card name should be the first line
                string[] updateCardLines = updateCard.Split(Environment.NewLine);

                try
                {
                    Console.WriteLine($"Updating {updateCardLines[0]}");

                    // New way - use the name given to look up the ID.  Use the ID to see if it has any children.  Recurse through that until we're out of cards to load.

                    // find the card objects.
                    List<Card> cardsToUpdate = new List<Card>();
                    bool cardFindError = false;

                    string cardName = updateCardLines[0];

                    Card cardToUpdateRequest = oldCardObjects.Where(x => x.name.Equals(cardName)).FirstOrDefault();

                    try
                    {
                        cardsToUpdate = FindCardsToUpdateRecursive(cardToUpdateRequest);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Can't find a card with name {cardName}, or one of it's children.  Check your spelling.  Exception:");
                        Console.WriteLine(ex.ToString());
                        cardFindError = true;
                    }

                    if (cardFindError)
                    {
                        continue;
                    }

                    List<XElement> cardXMLsToUpdate = new List<XElement>();

                    foreach (Card cardToUpdate in cardsToUpdate)
                    {
                        XElement cardXmlToUpdate = oldCardXmls[cardToUpdate.fileIndex - 1].XPathSelectElement($@"//unit[id='{cardToUpdate.id}']");

                        cardXMLsToUpdate.Add(cardXmlToUpdate);
                    }

                    //parse the ending attack, health, and delay from the update file
                    string[] updateStats = updateCardLines[2].Split('/');

                    int attack = -1;
                    int health = -1;
                    int delay = -1;

                    try
                    {
                        if (updateStats.Length == 3)
                        {
                            attack = int.Parse(updateStats[0]);
                            health = int.Parse(updateStats[1]);
                            delay = int.Parse(updateStats[2]);
                        }
                        else
                        {
                            health = int.Parse(updateStats[0]);
                            delay = int.Parse(updateStats[1]);
                        }
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine($"Failed to parse {updateCardLines[0]} - attack, health, or delay were not a number.");
                        Console.WriteLine(ex.Message);
                        cardsFailedToUpdate.Add(updateCardLines[0]);
                        continue;
                    }

                    // START REWRITE

                    for (int i = 0; i < cardXMLsToUpdate.Count; i++)
                    {
                        // get the card (can't foreach because we need to key on the index)
                        XElement cardXmlToUpdate = cardXMLsToUpdate[i];

                        // build the scaling factor
                        int tiersBelowMax = (cardXMLsToUpdate.Count - 1) - i;
                        double tierScaling = ((tiersBelowMax * config.AutoStats.PercentBetweenTiers) + (tiersBelowMax * config.AutoStats.PercentAcrossTier)) * 0.01;

                        // summons need different tier scaling
                        double summonTierScaling = ((double)i / cardXMLsToUpdate.Count);

                        // get the upgrades
                        List<XElement> upgrades = cardXmlToUpdate.XPathSelectElements("upgrade").ToList();
                        cardXmlToUpdate.XPathSelectElements("upgrade").Remove();

                        // build the level scaling factor
                        int levelsBelowMax = (upgrades.Count);  // Don't need a -1, as there's one extra that's at the card level that cancels out the off-by-one
                        double levelScaling = (levelsBelowMax * config.AutoStats.PercentAcrossTier * 0.01) / upgrades.Count;

                        double summonLevelScaling = ((double)1 / cardXMLsToUpdate.Count) * (1 - ((double)levelsBelowMax / upgrades.Count));

                        double scalingFactor = (1 - (tierScaling + levelScaling));
                        double summonScalingFactor = summonLevelScaling + summonTierScaling;

                        // Level 1 is on the card
                        // remove the delay node, and add it if needed.
                        if (cardXmlToUpdate.XPathSelectElement("cost") != null)
                        {
                            cardXmlToUpdate.XPathSelectElement("cost")?.Remove();

                            cardXmlToUpdate.Add(new XElement("cost", delay));
                        }

                        // Base card healths and attacks
                        cardXmlToUpdate.XPathSelectElement("health")?.Remove();

                        cardXmlToUpdate.Add(new XElement("health", Convert.ToInt32(Math.Floor(scalingFactor * health))));

                        // remove the attack node, and replace it if needed.
                        if (cardXmlToUpdate.XPathSelectElement("attack") != null)
                        {
                            cardXmlToUpdate.XPathSelectElement("attack")?.Remove();

                            cardXmlToUpdate.Add(new XElement("attack", Convert.ToInt32(Math.Floor(scalingFactor * attack))));
                        }

                        List<XElement> outputSkills = new List<XElement>();

                        //For skills, it's not so simple.
                        for (int j = 3; j < updateCardLines.Length; j++)
                        {
                            // the last X lines are skills.  Back-convert to the skill ID
                            // Base them off of the last card.
                            outputSkills.Add(BuildSingleSkillXml(StandardizeInputString(updateCardLines[j]), scalingFactor, summonScalingFactor));
                        }

                        cardXmlToUpdate.XPathSelectElements("skill").Remove();

                        cardXmlToUpdate.Add(outputSkills.ToArray());

                        // Levels 2+ are in the upgrades.

                        for (int k = 0; k < upgrades.Count; k++)
                        {
                            XElement upgrade = upgrades[k];

                            // build the level scaling factor
                            levelsBelowMax = (upgrades.Count - (k + 1));  // Don't need a -1, as there's one extra that's at the card level that cancels out the off-by-one
                            levelScaling = (levelsBelowMax * config.AutoStats.PercentAcrossTier * 0.01) / upgrades.Count;

                            summonLevelScaling = ((double)1 / cardXMLsToUpdate.Count) * (1 - ((double)levelsBelowMax / upgrades.Count));

                            scalingFactor = (1 - (tierScaling + levelScaling));
                            summonScalingFactor = summonLevelScaling + summonTierScaling;


                            // Base card healths and attacks
                            upgrade.XPathSelectElement("health")?.Remove();

                            upgrade.Add(new XElement("health", Convert.ToInt32(Math.Floor(scalingFactor * health))));

                            // remove the attack node, and replace it if needed.
                            if (attack != -1)
                            {
                                if (upgrade.XPathSelectElement("attack") != null)
                                {
                                    upgrade.XPathSelectElement("attack")?.Remove();
                                }

                                upgrade.Add(new XElement("attack", Convert.ToInt32(Math.Floor(scalingFactor * attack))));
                            }

                            List<XElement> upgradeOutputSkills = new List<XElement>();

                            //For skills, it's not so simple.
                            for (int l = 3; l < updateCardLines.Length; l++)
                            {
                                // the last X lines are skills.  Back-convert to the skill ID
                                // Base them off of the last card.
                                upgradeOutputSkills.Add(BuildSingleSkillXml(StandardizeInputString(updateCardLines[l]), scalingFactor, summonScalingFactor));
                            }

                            upgrade.XPathSelectElements("skill").Remove();

                            upgrade.Add(upgradeOutputSkills.ToArray());

                            cardXmlToUpdate.Add(upgrade);
                        }

                        newXmls.Add(int.Parse(cardXmlToUpdate.XPathSelectElement("id").Value), cardXmlToUpdate);
                    }

                    // END REWRITE

                    // Add the file for card ID to the end?
                    foreach (Card cardToUpdate in cardsToUpdate)
                    {
                        // find the root card xml
                        fileForCardId.Add(cardToUpdate.id, cardToUpdate.fileIndex);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    //Console.WriteLine(ex.StackTrace);
                    cardsFailedToUpdate.Add(updateCardLines[0]);
                }

            }

            SaveCards(cardsFailedToUpdate, newXmls, fileForCardId);
        }

        private void Initialize(ConfigStore config)
        {
            this.config = config;

            //Create the folder for the run.
            Directory.CreateDirectory(outputDirectory);

            string skillDataString = File.ReadAllText($@"{oldXmlDirectory}//skills_set.xml");
            XDocument skillDataDoc = XDocument.Parse(skillDataString);
            Updater.skillData = skillDataDoc.XPathSelectElements("//root/skillType").ToList();
            Updater.factionData = skillDataDoc.XPathSelectElements("//root/unitType").ToList();

            // get the fusion data, to replace how we're loading cards to change.
            string fusionDataString = File.ReadAllText($@"{oldXmlDirectory}//fusion_recipes_cj2.xml");
            XDocument fusionDataDoc = XDocument.Parse(fusionDataString);
            Updater.fusionData = fusionDataDoc.XPathSelectElements("//root/fusion_recipe").ToList();
        }

        private void SaveCards(List<string> cardsFailedToUpdate, Dictionary<int, XElement> newXmls, Dictionary<int, int> fileForCardId)
        {
            int cardsUpdated = 0;

            // we have a list of all the cards to update, and the files they're in.
            // for each file, check for updates, and if there are some, do them and then write to file.
            for (int i = 0; i < oldCardXmls.Count; i++)
            {
                bool hasUpdate = false;

                foreach (int cardId in fileForCardId.Keys)
                {
                    if (fileForCardId[cardId] == i + 1)
                    {
                        hasUpdate = true;
                    }
                }

                if (hasUpdate)
                {
                    // find the card IDs where this file is relevant.
                    foreach (int cardId in fileForCardId.Keys)
                    {
                        if (fileForCardId[cardId] == i + 1)
                        {
                            // get the old XML and delete it.
                            XElement newXml = newXmls[cardId];
                            string cardName = newXml.XPathSelectElement("name")?.Value.ToString();

                            //add the new node
                            oldCardXmls[i].XPathSelectElement($@"//unit[id='{cardId}']")?.ReplaceWith(newXml);

                            // Write to new file in the folder.
                            File.WriteAllText($"{outputDirectory}//{cardName}.xml", newXml.ToString());
                        }
                    }

                    // write file to disk, replacing existing.

                    string filename = $@"cards_section_{i + 1}.xml";
                    File.WriteAllText($@"{oldXmlDirectory}//{filename}", oldCardXmls[i].ToString());
                    cardsUpdated++;

                    Console.WriteLine($"file {i + 1} had changes.");
                }
            }

            Console.WriteLine($"{cardsUpdated} updated.  The following cards failed to update:");

            foreach (string failedCard in cardsFailedToUpdate)
            {
                Console.WriteLine(failedCard);
            }
        }

        private void LoadOldCards(string oldXmlDirectory)
        {
            // load the XML files
            oldCardXmls = new List<XDocument>();

            Console.WriteLine("Reading old XMLs from file");

            // load old XMLs
            int counter = 1;
            bool lastLoadFailed = false;
            do
            {
                //try to load the file at the counter location.  If it exists, keep going.  If it doesn't, then stop
                string filename = $@"cards_section_{counter}.xml";

                try
                {
                    string file = File.ReadAllText($@"{oldXmlDirectory}//{filename}");
                    XDocument fileXml = XDocument.Parse(file);
                    oldCardXmls.Add(fileXml);
                    counter++;
                }
                catch (Exception e)
                {
                    lastLoadFailed = true;
                    Console.WriteLine(e.Message);
                }
            } while (!lastLoadFailed);

            Console.WriteLine($@"oldXmlDirectory found {oldCardXmls.Count} files");

            Console.WriteLine("Parsing old XMLs into card XMLs.");

            oldCardObjects = new List<Card>();

            for (int i = 0; i < oldCardXmls.Count(); i++)
            {
                XDocument oldCardXml = oldCardXmls[i];
                List<XElement> extractedCards = oldCardXml.XPathSelectElements("//root/unit").ToList();

                //immediately extract them into cards, while we have the index.
                foreach (XElement cardXml in extractedCards)
                {
                    oldCardObjects.Add(new Card(cardXml, i + 1));
                }
            }

            Console.WriteLine($@"Found {oldCardObjects.Count} cards in old XML.");
        }

        private List<Card> FindCardsToUpdateRecursive(Card card)
        {
            // find the card objects.
            List<Card> cardsToUpdate = new List<Card>();

            if (card != null)
            {
                // basic XPath search to find the recipies that can create this card
                List<XElement> relevantRecipies = fusionData.Where(x => x.XPathSelectElement("card_id").Value.Equals(card.id.ToString())).ToList();

                if (relevantRecipies.Count > 0)
                {
                    // Do any of the ones have only a double of one other card?
                    foreach (XElement recipe in relevantRecipies)
                    {
                        List<XElement> resources = recipe.XPathSelectElements("resource").ToList();

                        if (resources.Count == 1 && resources.First().Attributes("number")?.FirstOrDefault()?.Value == "2")
                        {
                            Card cardToUpdate = oldCardObjects.Where(x => x.id.Equals(int.Parse(resources.First().Attributes("card_id").First().Value)) || x.upgradeLevels.Values.Where(y => y.id.Equals(int.Parse(resources.First().Attributes("card_id").First().Value))).ToList().Count >= 1).FirstOrDefault();

                            cardsToUpdate = FindCardsToUpdateRecursive(cardToUpdate);
                        }
                    }
                }

                Console.WriteLine($"Found child card {card.name}");
                cardsToUpdate.Add(card);
            }

            return cardsToUpdate;
        }

        private XElement BuildSingleSkillXml(string skillString, double scalingFactor, double summonScalingFactor)
        {
            // get the card data to build
            OutputSkillData skillData = ProcessSingleSkill(skillString, scalingFactor, summonScalingFactor);

            XElement newSkillXml = new XElement("skill");

            newSkillXml.SetAttributeValue("id", skillData.skillId);

            if (skillData.x != null)
            {
                newSkillXml.SetAttributeValue("x", skillData.x);
            }

            if (skillData.y != null)
            {
                newSkillXml.SetAttributeValue("y", skillData.y);
            }

            if (skillData.all != null)
            {
                newSkillXml.SetAttributeValue("all", skillData.all);
            }

            if (skillData.c != null)
            {
                newSkillXml.SetAttributeValue("c", skillData.c);
            }

            if (skillData.s != null)
            {
                newSkillXml.SetAttributeValue("s", skillData.s);
            }

            if (skillData.s2 != null)
            {
                newSkillXml.SetAttributeValue("s2", skillData.s2);
            }

            if (skillData.n != null)
            {
                newSkillXml.SetAttributeValue("n", skillData.n);
            }

            if (skillData.card_id != null)
            {
                newSkillXml.SetAttributeValue("card_id", skillData.card_id);
            }

            if (skillData.trigger != null)
            {
                newSkillXml.SetAttributeValue("trigger", skillData.trigger);
            }

            return newSkillXml;
        }

        private string StandardizeInputString(string inputString)
        {
            string workingString = inputString;

            // run the string through the standardization dictionary
            foreach (string standardKey in standardizationDictionary.Keys)
            {
                workingString = workingString.Replace(standardKey, standardizationDictionary[standardKey], StringComparison.InvariantCultureIgnoreCase);
            }

            // run the string through the skill sheet, to convert everything to IDs.
            foreach (XElement skillData in Updater.skillData)
            {
                string name = skillData.XPathSelectElement("name").Value;
                string id = skillData.XPathSelectElement("id").Value;

                if (name != null && id != null && !name.Equals("Attack", StringComparison.InvariantCultureIgnoreCase))
                {
                    workingString = workingString.Replace(name, id, StringComparison.InvariantCultureIgnoreCase);
                }
            }

            return workingString;
        }


        private OutputSkillData ProcessSingleSkill(string fullSkillString, double scalingFactor, double summonScalingFactor)
        {
            string[] skillString = fullSkillString.Split(' ');

            string skillId = skillString[0];

            List<string> factionList = new List<string>() { "Imperial", "Raider", "Bloodthirsty", "Xeno", "Righteous", "Progenitor" };

            int? newValue = null;
            int? newFaction = null;
            int? newAll = null;
            int? newCooldown = null;
            string? newSkill = null;
            string? newSkill2 = null;
            int? newNumber = null;
            Card summonCard = null;
            string? newTrigger = null;

            int? scaledValue = null;
            int? scaledNumber = null;

            int factionMod = 0;

            // check the last part of the split for trigger words.
            List<string> triggerWords = new List<string>() { "attacked", "play", "death" };

            if (triggerWords.Contains(skillString[skillString.Length - 1]))
            {
                newTrigger = skillString[skillString.Length - 1];

                // we got a trigger, truncate it so everything else doesn't break.
                skillString = skillString.Take(skillString.Length - 1).ToArray();
            }

            switch (skillId)
            {
                // for Wall and Rush, there are none.
                case "wall":
                case "rush":
                    //cool.
                    break;
                // for flurry and mimic, there are two, value and cooldown
                case "flurry":
                case "mimic":
                    switch (skillString.Length)
                    {
                        case 1:
                            //e.g. "Flurry".  Assume 1 value, no cooldown.
                            newValue = 1;

                            break;
                        case 2:
                            //e.g. "Mimic 50".  Assume value X, no cooldown.
                            newValue = int.Parse(skillString[1]);

                            break;
                        case 3:
                            // e.g. Flurry every 6.  Assume no value, X cooldown
                            newCooldown = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // e.g. Flurry 2 every 4.  Assume X/Y
                            newValue = int.Parse(skillString[1]);
                            newCooldown = int.Parse(skillString[3]);

                            break;
                        default:
                            // No idea how we could get here.
                            break;
                    }
                    break;
                // two, value and all
                case "strike":
                case "weaken":
                case "siege":
                case "enfeeble":
                case "besiege":
                case "sunder":
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                        newValue = int.Parse(skillString[2]);
                    }
                    else
                    {
                        newAll = 0;
                        newValue = int.Parse(skillString[1]);
                    }

                    break;
                // three params, value, faction, and all
                case "heal":
                case "rally":
                case "protect":
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                    }
                    else
                    {
                        newAll = 0;
                    }

                    factionMod = 0;

                    if (factionList.Contains(skillString[1 + newAll.Value]))
                    {
                        XElement faction = factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[1 + newAll.Value])).FirstOrDefault();
                        newFaction = int.Parse(faction.XPathSelectElement("id").Value);

                        factionMod = 1;
                    }

                    newValue = int.Parse(skillString[1 + newAll.Value + factionMod]);

                    break;
                // three params (all, cooldown, number)
                case "jam":
                    switch (skillString.Length)
                    {
                        case 2:
                            //e.g. Jam 4, or Jam all.  One value is either all or number, no CD.
                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            break;
                        case 3:
                            // e.g. Jam every 5.  One value is the cooldown

                            newCooldown = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // e.g. Jam all every 4.  First is number or all, second is cooldown.

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            newCooldown = int.Parse(skillString[3]);

                            break;
                        default:
                            // no idea what happened here.
                            break;
                    }

                    break;
                // three params (faction, all, number)
                case "overload":
                    // 1 is either a number or all.
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                    }
                    else
                    {
                        newNumber = int.Parse(skillString[1]);
                    }

                    if (skillString.Length >= 3 && factionList.Contains(skillString[2]))
                    {
                        XElement faction = factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[2])).FirstOrDefault();
                        newFaction = int.Parse(faction.XPathSelectElement("id").Value);
                    }

                    break;
                // four params (value, faction, all, number)
                case "enrage":
                case "entrap":
                    switch (skillString.Length)
                    {
                        case 2:
                            //e.g. both must have a value, so this is that.
                            newValue = int.Parse(skillString[1]);

                            break;
                        case 3:
                            // This could either be all, a number, or a faction.  Third value is always a value, though.
                            newValue = int.Parse(skillString[2]);

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else if (factionList.Contains(skillString[1]))
                            {
                                XElement faction = factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[1])).FirstOrDefault();
                                newFaction = int.Parse(faction.XPathSelectElement("id").Value);
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            break;
                        case 4:
                            // Has either all or number, a faction, and a value.
                            newValue = int.Parse(skillString[3]);

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            XElement factionDefault = factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[2])).FirstOrDefault();
                            newFaction = int.Parse(factionDefault.XPathSelectElement("id").Value);

                            break;
                        default:

                            break;
                    }

                    break;
                // four params (value, all, skill, number)
                case "enhance":
                    switch (skillString.Length)
                    {
                        case 3:
                            // Enhance skill value
                            newSkill = skillString[1];

                            newValue = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // Enhance all/number skill value
                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            newSkill = skillString[2];

                            newValue = int.Parse(skillString[3]);

                            break;
                        default:

                            break;
                    }

                    break;
                // four (all, skill 1, skill 2, number)
                case "evolve":
                    switch (skillString.Length)
                    {
                        case 3:
                            // evolve skill1 to skill2
                            // convert the skill to an ID
                            newSkill = skillString[1];

                            newSkill2 = skillString[3];

                            break;
                        case 4:
                            // evolve number/all skill1 to skill2

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            // convert the skill to an ID
                            newSkill = skillString[2];

                            newSkill2 = skillString[4];
                            break;
                        default:

                            break;
                    }
                    break;
                // summon sucks
                case "summon":
                    //Hoo boy.  Here we go.
                    // The first segment will be the Summon keyword.  The last, if it's a trigger, is by necessity, the trigger, and is removed earlier.
                    // So, to get the card name of the summon, we can concatinate everything else?
                    string[] nameSections = skillString.Skip(1).Take(skillString.Length - 1).ToArray();
                    string summonCardName = string.Join(" ", nameSections);

                    summonCard = oldCardObjects.Where(x => x.name.Equals(summonCardName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (summonCard == null)
                    {
                        Console.WriteLine($"Can't find summon card \"{summonCardName}\"");
                        throw new Exception($"Can't find summon card \"{summonCardName}\"");
                    }
                    // With the summon card retrieved, we'll worry about scaling it below.

                    break;
                // everything else just has one param, value
                default:
                    newValue = int.Parse(skillString[1]);

                    break;
            }

            // scale the values.
            if (newValue != null)
            {
                scaledValue = Convert.ToInt32(Math.Floor(newValue.Value * scalingFactor));
            }

            if (newNumber != null)
            {
                scaledNumber = Convert.ToInt32(Math.Floor(newNumber.Value * scalingFactor));
            }

            int? cardId = null;

            // scale the summon card ID
            if (summonCard != null)
            {
                int levelCount = summonCard.upgradeLevels.Count; // include the base card

                int scaledLevelCount = Convert.ToInt32(Math.Floor((levelCount - 1) * summonScalingFactor)) + 1;

                if (scaledLevelCount == 1)
                {
                    // base card ID
                    cardId = summonCard.id;
                }
                else
                {
                    // upgrade level
                    cardId = summonCard.upgradeLevels[scaledLevelCount].id; // watch this - I think I indexed by level but am not sure.
                }
            }

            return new OutputSkillData(skillId, scaledValue, newFaction, newAll, newCooldown, newSkill, newSkill2, scaledNumber, newTrigger, cardId);


        }
    }
}
