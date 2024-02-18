using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using TU_Card_XML_Gen;

namespace TUComparatorLibrary
{
    public class Updater
    {
        public static List<XElement> skillData;
        public static List<XElement> factionData;

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
            { " imp", " Imperial" },
            { " rd", " Raider" },
            { " bt", " Bloodthirsty" },
            { " rt", " Righteous" },
            { " prog", " Progenitor" },
            { " OnPlay", " play" },
            { " On Play:", " play" },
            { " On Play", " play" },
            { " OP", " play" },
            { " OnAttacked", " attacked" },
            { " On Attacked:", " attacked" },
            { " On Attacked", " attacked" },
            { " OA", " attacked" },
            { " OnDeath", " death" },
            { " On Death:", " death" },
            { " On Death", " death" },
            { " OD", " death" },
            { " zerk", " berserk" }
        };

        public void Run(string oldXmlDirectory, string updateFilePath)
        {
            string skillData = File.ReadAllText($@"{oldXmlDirectory}//skills_set.xml");
            XDocument skillDataDoc = XDocument.Parse(skillData);
            Updater.skillData = skillDataDoc.XPathSelectElements("//root/skillType").ToList();
            Updater.factionData = skillDataDoc.XPathSelectElements("//root/unitType").ToList();

            List<string> cardsFailedToUpdate = new List<string>();
            int cardsUpdated = 0;

            // load the XML files
            List<XDocument> oldCardXmls = new List<XDocument>();

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

            List<Card> oldCardObjects = new List<Card>();

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


            // Load the update file.

            string updateFileContents = File.ReadAllText(updateFilePath);

            // split the file into cards to find.
            string[] updateCards = updateFileContents.Split($@"{Environment.NewLine}{Environment.NewLine}");

            Dictionary<int, XElement> newXmls = new Dictionary<int, XElement>();
            Dictionary<int, int> fileForCardId = new Dictionary<int, int>();

            foreach (string updateCard in updateCards)
            {
                // the card name should be the first line
                string[] updateCardLines = updateCard.Split(Environment.NewLine);

                try
                {
                    Console.WriteLine($"Updating {updateCardLines[0]}");

                    // ASSUMPTION: Card names will be in fusion order.  First one will be the lowest to update, and contain the level 1 to scale from.
                    string[] cardNames = updateCardLines[0].Split("|");

                    // find the card objects.
                    List<Card> cardsToUpdate = new List<Card>();
                    bool cardFindError = false;

                    foreach (string cardName in cardNames)
                    {
                        Card cardToUpdate = oldCardObjects.Where(x => x.name.Equals(cardName)).FirstOrDefault();

                        if (cardToUpdate != null)
                        {
                            cardsToUpdate.Add(cardToUpdate);
                        }
                        else
                        {
                            Console.WriteLine($"Can't find a card with name {cardName}.  Check your spelling.");
                            cardsFailedToUpdate.Add(updateCardLines[0]);
                            cardFindError = true;
                        }

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


                    // get the level 1 stats from the card for scaling
                    int baseAttack = cardsToUpdate[0].upgradeLevels[1].attack;
                    int baseHealth = cardsToUpdate[0].upgradeLevels[1].health;

                    // level 1 stays the same, level 6 is the new value.  Scale the other 4 values.
                    // The arrays are now more complicated.  We need 6 levels for each card we're updating.
                    int[] levelAttacks = new int[cardsToUpdate.Count * 6];
                    int[] levelHealths = new int[cardsToUpdate.Count * 6];

                    for (int i = 0; i < levelAttacks.Length; i++)
                    {
                        // scaling factor is (i as % of length - 1)
                        double scalingFactor = (double)i / (levelAttacks.Length - 1);

                        levelAttacks[i] = (int)Math.Floor(baseAttack + (scalingFactor * (attack - baseAttack)));
                        levelHealths[i] = (int)Math.Floor(baseHealth + (scalingFactor * (health - baseHealth)));
                    }

                    List<OutputSkillData> outputSkills = new List<OutputSkillData>();

                    //For skills, it's not so simple.
                    for (int i = 3; i < updateCardLines.Length; i++)
                    {
                        // the last X lines are skills.  Back-convert to the skill ID
                        // Base them off of the last card.
                        outputSkills.Add(ProcessSingleSkill(cardsToUpdate, StandardizeInputString(updateCardLines[i])));
                    }

                    int outputSkillIndex = 0;

                    foreach (XElement cardXmlToUpdate in cardXMLsToUpdate)
                    {
                        // we have the attacks, healths, and skills.  Time to modify some XML.
                        // level 1 stays the same.  For everything else, it's time to build some upgrade nodes.
                        List<XElement> upgrades = cardXmlToUpdate.XPathSelectElements("upgrade").ToList();

                        cardXmlToUpdate.XPathSelectElements("upgrade").Remove();

                        // remove the delay node, and add it if needed.
                        if (cardXmlToUpdate.XPathSelectElement("cost") != null)
                        {
                            cardXmlToUpdate.XPathSelectElement("cost")?.Remove();

                            cardXmlToUpdate.Add(new XElement("cost", delay));
                        }

                        XElement workingCardXmlToUpdate;

                        // TODO: we have to handle level 1, now that it can change.
                        // Still don't want to change the first card
                        if(outputSkillIndex > 0)
                        {
                            // Base card healths and attacks
                            cardXmlToUpdate.XPathSelectElement("health")?.Remove();

                            cardXmlToUpdate.Add(new XElement("health", levelHealths[outputSkillIndex]));

                            // remove the attack node, and replace it if needed.
                            if (cardXmlToUpdate.XPathSelectElement("attack") != null)
                            {
                                cardXmlToUpdate.XPathSelectElement("attack")?.Remove();

                                cardXmlToUpdate.Add(new XElement("attack", levelAttacks[outputSkillIndex]));
                            }


                            // remove the existing skills, preserving any summons.  Build a new XElement.
                            List<XElement> summons = cardXmlToUpdate.XPathSelectElements("skill[@id='summon']").ToList();
                            // also preserve any non-summons, in case the skill is marked "do not change"
                            List<XElement> nonsummons = cardXmlToUpdate.XPathSelectElements("skill[@id!='summon']").ToList();

                            // this method is absolutely chok-full of side-effects anyways.  Do I even need the output?
                            workingCardXmlToUpdate = ProcessCardSkills(cardXmlToUpdate, summons, nonsummons, outputSkills, updateCardLines, outputSkillIndex);
                        }
                        else
                        {
                            workingCardXmlToUpdate = cardXmlToUpdate;
                        }


                        // after handling level 1:
                        outputSkillIndex++;

                        // Most skills work fine.  Summon is fucked.
                        // for skills, if one is specified, they all must be.  We generate all skills except summon by default.
                        // to prepare for filling in missing summons, get the level 1 summons, if present, then set how many summons we expect on the card.
                        // if any future level finds fewer summons than this, use the last level's summons instead.

                        List<XElement> baseSummons = workingCardXmlToUpdate.XPathSelectElements("skill[@id='summon']").ToList();
                        List<XElement> lastLevelSummons = baseSummons;

                        foreach (XElement upgrade in upgrades)
                        {
                            // get the level
                            int level = int.Parse(upgrade.XPathSelectElement("level").Value);

                            // remove the health node, and replace it if needed.
                            upgrade.XPathSelectElement("health")?.Remove();

                            upgrade.Add(new XElement("health", levelHealths[outputSkillIndex]));

                            // remove the attack node, and replace it if needed.
                            if (workingCardXmlToUpdate.XPathSelectElement("attack") != null)
                            {
                                upgrade.XPathSelectElement("attack")?.Remove();

                                upgrade.Add(new XElement("attack", levelAttacks[outputSkillIndex]));
                            }

                            // remove the existing skills, preserving any summons.  Build a new XElement.
                            List<XElement> summons = upgrade.XPathSelectElements("skill[@id='summon']").ToList();
                            // also preserve any non-summons, in case the skill is marked "do not change"
                            List<XElement> nonsummons = upgrade.XPathSelectElements("skill[@id!='summon']").ToList();

                            // use last level's summons if there are none here.  If there are some here, set this as the last level's summons to check on the next level.
                            if (summons.Count >= baseSummons.Count)
                            {
                                lastLevelSummons = summons;
                            }
                            else
                            {
                                summons = lastLevelSummons;
                            }

                            // Do the skills (now a method, because we need it elsewhere as well)
                            workingCardXmlToUpdate.Add(ProcessCardSkills(upgrade, summons, nonsummons, outputSkills, updateCardLines, outputSkillIndex));

                            outputSkillIndex++;
                        }

                        newXmls.Add(int.Parse(workingCardXmlToUpdate.XPathSelectElement("id").Value), workingCardXmlToUpdate);
                    }

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
                    //Console.WriteLine(ex.StackTrace);
                    cardsFailedToUpdate.Add(updateCardLines[0]);
                }

            }

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

                            oldCardXmls[i].XPathSelectElement($@"//unit[id='{cardId}']")?.Remove();

                            //add the new node
                            oldCardXmls[i].XPathSelectElement("//root").Add(newXml);
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

        private static XElement BuildSingleSkillXml(int level, OutputSkillData outputSkillData)
        {
            if (outputSkillData.skillId != "summon")
            {
                XElement newSkillXml = new XElement("skill");

                newSkillXml.SetAttributeValue("id", outputSkillData.skillId);

                if (outputSkillData.x != null)
                {
                    newSkillXml.SetAttributeValue("x", outputSkillData.x[level]);
                }

                if (outputSkillData.y != null)
                {
                    newSkillXml.SetAttributeValue("y", outputSkillData.y[level]);
                }

                if (outputSkillData.all != null)
                {
                    newSkillXml.SetAttributeValue("all", outputSkillData.all[level]);
                }

                if (outputSkillData.c != null)
                {
                    newSkillXml.SetAttributeValue("c", outputSkillData.c[level]);
                }

                if (outputSkillData.s != null)
                {
                    newSkillXml.SetAttributeValue("s", outputSkillData.s[level]);
                }

                if (outputSkillData.s2 != null)
                {
                    newSkillXml.SetAttributeValue("s2", outputSkillData.s2[level]);
                }

                if (outputSkillData.n != null)
                {
                    newSkillXml.SetAttributeValue("n", outputSkillData.n[level]);
                }

                if (outputSkillData.trigger != null)
                {
                    newSkillXml.SetAttributeValue("trigger", outputSkillData.trigger[level]);
                }

                return newSkillXml;
            }

            return null;
        }

        private static string StandardizeInputString(string inputString)
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

        private static XElement ProcessCardSkills(XElement rootElement, List<XElement> summons, List<XElement> nonsummons, List<OutputSkillData> outputSkills, string[] updateCardLines, int outputSkillIndex)
        {
            rootElement.XPathSelectElements("skill")?.Remove();

            foreach (string skillOrder in skillKeywordOrder)
            {
                if (keywordOrder.Contains(skillOrder))
                {
                    // do the trigger skills.
                    // still need to keep the same order.  We're O(n^2) now, folks.
                    foreach (string innerSkillOrder in skillKeywordOrder)
                    {
                        // skip the keywords this time, we're just trying to figure out the order of the non-keyworded skills
                        if (!keywordOrder.Contains(innerSkillOrder))
                        {
                            if (innerSkillOrder.Equals("summon", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // copy over the summons with the correct trigger type
                                foreach (XElement summon in summons)
                                {
                                    if (summon.Attribute("trigger") != null && summon.Attribute("trigger").Value.Equals(skillOrder, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        rootElement.Add(summon);
                                    }
                                }
                            }
                            else
                            {
                                foreach (OutputSkillData outputSkillData in outputSkills)
                                {
                                    if (outputSkillData == null)
                                    {
                                        throw new NullReferenceException($"{updateCardLines[0]} - null skill data.  Check your input skills.");
                                    }

                                    //check for triggers
                                    if (outputSkillData.trigger != null && outputSkillData.trigger.Length > 0 && outputSkillData.trigger[outputSkillData.trigger.Length - 1].Equals(skillOrder, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // the trigger matches.  Does the skill?
                                        if (outputSkillData.skillId.Equals(innerSkillOrder, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            List<XElement> preserved = nonsummons.Where(x => x.Attribute("id").Value.Equals(innerSkillOrder) && !string.IsNullOrEmpty(x.Attribute("trigger")?.Value) && x.Attribute("trigger").Value.Equals(skillOrder, StringComparison.InvariantCultureIgnoreCase)).ToList();
                                            // is this level one to skip?
                                            if (outputSkillData.skipThisLevel[outputSkillIndex])
                                            {
                                                if (preserved.Count == 1)
                                                {
                                                    rootElement.Add(preserved[0]);
                                                }
                                                else if (preserved.Count > 1)
                                                {
                                                    throw new ArgumentException($"Skill {innerSkillOrder} with trigger {skillOrder} on card was not supposed to be updated at overall level {outputSkillIndex}, but there were 2 old matching skills.  Code is confused.  Skipping card, check your inputs.");
                                                }
                                            }
                                            else
                                            {
                                                // both match.  Add the skill
                                                rootElement.Add(BuildSingleSkillXml(outputSkillIndex, outputSkillData));
                                            }

                                            if (preserved.Count > 0)
                                            {
                                                nonsummons.Remove(preserved[0]);
                                            }
                                        }
                                    }
                                }

                                //preserve skills for which there is no matching output data.
                                List<XElement> preservedNotPresent = nonsummons.Where(x => (x.Attribute("id")?.Value ?? "").Equals(innerSkillOrder) && (x.Attribute("trigger")?.Value ?? "").Equals(skillOrder)).ToList();
                                foreach (XElement preserved in preservedNotPresent)
                                {
                                    if (outputSkills.Where(x => x.skillId.Equals(preserved.Attribute("id")?.Value) && x.trigger.Equals(preserved.Attribute("trigger")?.Value)).FirstOrDefault() == null)
                                    {
                                        // There is no match, preserve it.

                                        rootElement.Add(preserved);

                                        nonsummons.Remove(preserved);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (skillOrder.Equals("summon", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // copy over the non-triggered summons from the preserved summon XMLs
                        foreach (XElement summon in summons)
                        {
                            if (summon.Attribute("trigger") == null)
                            {
                                rootElement.Add(summon);
                            }
                        }
                    }
                    else
                    {
                        // non-trigger skills, in order.
                        foreach (OutputSkillData outputSkillData in outputSkills)
                        {
                            if (outputSkillData == null)
                            {
                                Console.WriteLine($"{updateCardLines[0]} - null skill data.  Check your input skills.");
                                throw new NullReferenceException($"{updateCardLines[0]} - null skill data.  Check your input skills.");
                            }

                            if (outputSkillData.trigger == null || outputSkillData.trigger.Length == 0)
                            {
                                // not a summon or trigger skill
                                if (outputSkillData.skillId.Equals(skillOrder, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    List<XElement> preserved = nonsummons.Where(x => x.Attribute("id").Value.Equals(skillOrder) && String.IsNullOrEmpty(x.Attribute("trigger")?.Value)).ToList();
                                    // is this level one to skip?
                                    if (outputSkillData.skipThisLevel[outputSkillIndex])
                                    {
                                        if (preserved.Count == 1)
                                        {
                                            rootElement.Add(preserved[0]);
                                        }
                                        else if (preserved.Count > 1)
                                        {
                                            throw new ArgumentException($"Skill {skillOrder} on card was not supposed to be updated at overall level {outputSkillIndex}, but there were 2 old matching skills.  Code is confused.  Skipping card, check your inputs.");
                                        }

                                        if (preserved.Count > 0)
                                        {
                                            nonsummons.Remove(preserved[0]);
                                        }
                                    }
                                    else
                                    {
                                        // matches - copy it over
                                        rootElement.Add(BuildSingleSkillXml(outputSkillIndex, outputSkillData));
                                    }
                                }
                            }
                        }

                        //preserve skills for which there is no matching output data.
                        List<XElement> preservedNotPresent = nonsummons.Where(x => (x.Attribute("id")?.Value ?? "").Equals(skillOrder) && String.IsNullOrEmpty(x.Attribute("trigger")?.Value)).ToList();
                        foreach(XElement preserved in preservedNotPresent)
                        {
                            if(outputSkills.Where(x => x.skillId.Equals(preserved.Attribute("id")?.Value) && x.trigger == null).FirstOrDefault() == null){
                                // There is no match, preserve it.

                                rootElement.Add(preserved);

                                nonsummons.Remove(preserved);
                            }
                        }
                    }
                }
            }

            // If the root element ended up with 4 or more skills somehow, throw an error.
            if (rootElement.XPathSelectElements("skill").Count() > 3)
            {
                throw new ArgumentException($"Card ended up with four skill lines.  This can happen if a card's skill was changed.  If changing a card's skill, the base XML must be changed first such that the system knows where the new skill starts.");
            }

            return rootElement;
        }

        private static OutputSkillData ProcessSingleSkill(List<Card> cardsToUpdate, string fullSkillString)
        {
            string[] skillString = fullSkillString.Split(' ');

            string skillId = skillString[0];

            List<string> factionList = new List<string>() { "Imperial", "Raider", "Bloodthirsty", "Xeno", "Righteous", "Progenitor" };

            // figure out what properties we're expecting.
            int[] x = null;
            int[] y = null;
            int[] all = null;
            int[] c = null;
            string[] s = null;
            string[] s2 = null;
            int[] n = null;
            string[] trigger = null;
            bool[] skipThisLevel = null;

            int newValue = -1;
            int newFaction = -1;
            int newAll = -1;
            int newCooldown = -1;
            string newSkill = string.Empty;
            string newSkill2 = string.Empty;
            int newNumber = -1;
            string newTrigger = string.Empty;

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

                    if (factionList.Contains(skillString[1 + newAll]))
                    {
                        XElement faction = Updater.factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[1 + newAll])).FirstOrDefault();
                        newFaction = int.Parse(faction.XPathSelectElement("id").Value);

                        factionMod = 1;
                    }

                    newValue = int.Parse(skillString[1 + newAll + factionMod]);

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
                        XElement faction = Updater.factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[2])).FirstOrDefault();
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
                                XElement faction = Updater.factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[1])).FirstOrDefault();
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

                            XElement factionDefault = Updater.factionData.Where(x => x.XPathSelectElement("name").Value.Equals(skillString[2])).FirstOrDefault();
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
                    //nope
                    break;
                // everything else just has one param, value
                default:
                    newValue = int.Parse(skillString[1]);

                    break;
            }

            // Does the new skill match the last level of the last card's existing skills?
            // TODO: Are we matching tags?
            // YES, we are matching tags, and we can use the parsed tags to check!
            bool skillIsOnLastLevel = cardsToUpdate[cardsToUpdate.Count - 1].upgradeLevels[cardsToUpdate[cardsToUpdate.Count - 1].upgradeLevels.Count - 1].skillList
                .Where(x => x.id.Equals(skillId, StringComparison.InvariantCultureIgnoreCase)
                    && x.faction.Equals(newFaction)
                    && x.trigger.Equals(newTrigger)
                    && x.all.Equals(newAll == 1) // all is a boolean, but newAll is an int
                ).Any();

            // What is the earliest level on which card does the skill match?
            int earliestLevelSkillExists = -1;

            int totalLevelsCounted = 0;

            for (int i = 0; i < cardsToUpdate.Count; i++)
            {
                Card currentCard = cardsToUpdate[i];

                for (int j = 1; j < currentCard.upgradeLevels.Count + 1; j++)
                {
                    UpgradeLevel currentLevel = currentCard.upgradeLevels[j];

                    // TODO: Are we matching tags?
                    // YES, we are matching tags, and we can use the parsed tags to check!
                    if (currentLevel.skillList
                        .Where(x => x.id.Equals(skillId, StringComparison.InvariantCultureIgnoreCase)
                            && x.faction.Equals(newFaction)
                            && x.trigger.Equals(newTrigger)
                            && x.all.Equals(newAll == 1) // all is a boolean, but newAll is an int
                        ).Any())
                    {

                        if (earliestLevelSkillExists == -1)
                        {
                            earliestLevelSkillExists = totalLevelsCounted;

                        }

                    }

                    totalLevelsCounted++;
                }
            }

            // Figure out how long we're going to have to stretch this value over.
            int endingArrayMaxLength = 0;

            foreach (Card currentCard in cardsToUpdate)
            {
                foreach (int upgradeLevel in currentCard.upgradeLevels.Keys)
                {
                    endingArrayMaxLength++;
                }
            }

            double baseScalingFactor = 1.0 / ((endingArrayMaxLength - earliestLevelSkillExists) - 1);

            // We have the end value, as well as the understanding of where we start and whether we can assume an end.  Let's get cooking.
            if (!skillIsOnLastLevel)
            {
                Console.WriteLine($"Skill {skillId} is not on the last level of the existing card.  Can't make an automatic decision as to how to handle this.  Modify level manually first.");
                throw new ArgumentException($"Skill {skillId} is not on the last level of the existing card.  Can't make an automatic decision as to how to handle this.  Modify level manually first.");
            }
            else
            {
                // Build a list of all the card skills across all the cards.
                List<UpgradeLevel> levels = new List<UpgradeLevel>();

                foreach (Card card in cardsToUpdate)
                {
                    foreach (int key in card.upgradeLevels.Keys)
                    {
                        levels.Add(card.upgradeLevels[key]);
                    }
                }

                Skill earliestInstance = levels[earliestLevelSkillExists].skillList.Where(x => x.id.Equals(skillId, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                // for each of the things we have a final value for, get the starting value and build a chart.
                if (newValue != -1)
                {
                    int j = 0;

                    int oldValue = earliestInstance.value;
                    x = new int[endingArrayMaxLength];
                    for (int i = 0; i < x.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            x[i] = (int)Math.Floor(oldValue + (j * baseScalingFactor * (newValue - oldValue)));
                            j++;
                        }
                        else
                        {
                            x[i] = -1;
                        }
                    }
                }

                if (newFaction != -1)
                {
                    y = new int[endingArrayMaxLength];
                    for (int i = 0; i < y.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            y[i] = newFaction;
                        }
                        else
                        {
                            y[i] = -1;
                        }
                    }
                }

                if (newAll != -1)
                {
                    all = new int[endingArrayMaxLength];
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            all[i] = newAll;
                        }
                        else
                        {
                            all[i] = -1;
                        }
                    }
                }

                if (newCooldown != -1)
                {
                    int j = 0;

                    int oldCooldown = earliestInstance.cooldown;
                    c = new int[endingArrayMaxLength];
                    for (int i = 0; i < c.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            c[i] = (int)Math.Floor(oldCooldown + (j * baseScalingFactor * (newCooldown - oldCooldown)));
                            j++;
                        }
                        else
                        {
                            c[i] = -1;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(newSkill))
                {
                    s = new string[endingArrayMaxLength];
                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            s[i] = newSkill;
                        }
                        else
                        {
                            s[i] = string.Empty;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(newSkill2))
                {
                    s2 = new string[endingArrayMaxLength];
                    for (int i = 0; i < s2.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            s2[i] = newSkill2;
                        }
                        else
                        {
                            s2[i] = string.Empty;
                        }
                    }
                }

                if (newNumber != -1)
                {
                    int j = 0;

                    int oldNumber = earliestInstance.number;

                    n = new int[endingArrayMaxLength];
                    for (int i = 0; i < n.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            n[i] = (int)Math.Floor(oldNumber + (j * baseScalingFactor * (newNumber - oldNumber)));
                            j++;
                        }
                        else
                        {
                            n[i] = -1;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(newTrigger))
                {
                    trigger = new string[endingArrayMaxLength];
                    for (int i = 0; i < trigger.Length; i++)
                    {
                        if (i >= earliestLevelSkillExists)
                        {
                            trigger[i] = newTrigger;
                        }
                        else
                        {
                            trigger[i] = string.Empty;
                        }
                    }

                }

                skipThisLevel = new bool[endingArrayMaxLength];
                for (int i = 0; i < skipThisLevel.Length; i++)
                {
                    if (i >= earliestLevelSkillExists)
                    {
                        skipThisLevel[i] = false;
                    }
                    else
                    {
                        skipThisLevel[i] = true;
                    }
                }

                return new OutputSkillData(skillId, x, y, all, c, s, s2, n, trigger, skipThisLevel);
            }

            return null;

        }
    }
}
