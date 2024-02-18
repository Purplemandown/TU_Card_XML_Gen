In the config file, set the directory of your card XMLs, and the path to the updatefile.  Then run the exe.

Cards should have two spaces between them, and be formatted like:

Utopia Prayer
Righteous Mythic
20/200/3
Jam 4 OnPlay
Bravery 50
Refresh 60

Buildings and commanders should omit attack.  Always include delay, even if it makes no sense (set it to 0?)

Skills with no params:

Wall
Rush
Flying?


If you want to span the update across more than one card (e.g. multiple fusion levels of the same card), you'd do the following:

Benediction|Holy Benediction|Pure Benediction
Legendary Righteous
60/200/4
Evade 20
Rally All RT 100 OnAttacked
Mortar All 60

The lowest rank card should be first, and they should proceed in ascending order.  Note that the system does not check if the cards are actually different fusion ranks of one another, and will attempt to handle it if you span cards that aren't related, so this should be avoided.


Skills with one param (value):

Armor <value>
Counter <value>
Pierce <value>
Poison <value>
Leech <value>
Evade <value>
Berserk <value>
Corrosive <value>
Inhibit <value>
Valor <value>
Legion <value>
Payback <value>
Avenge <value>
Refresh <value>
Venom <value>
Mend <value>
Swipe <value>
Rupture <value>
Allegiance <value>
Drain <value>
Stasis <value>
Revenge <value>
Coalition <value>
Sabotage <value>
Barrier <value>
Subdue <value>
Tribute <value>
Bravery <value>
Absorb <value>
Disease <value>
Mark <value>
Fortify <value>
Hunt <value>
Scavenge <value>


Skills with two params (value, cooldown):

Flurry <value> every <cooldown>
Mimic <value> every <cooldown>


Skills with two params (value, all):

Strike <all> <value>
Weaken <all> <value>
Siege <all> <value>
Enfeeble <all> <value>
Mortar <all> <value>
Sunder <all> <value>


Skills with three params (value, faction, all)

Heal <all> <faction> <value>
Rally <all> <faction> <value>
Protect <all> <faction> <value>


Skills with three params (all, cooldown, number)

Jam <all/number> every <cooldown>

Skills with three params (faction, all, number)

Overload <all/number> <faction>


Skills with four params (value, faction, all, number)

Enrage <all/number> <faction> <value>
Entrap <all/number> <faction> <value>


Skills with four params (value, all, skill, number)

Enhance <all/number> <skill> <value>


Skills with four params (all, skill1, skill2, number):

Evolve <all/number> <skill1> to <skill2>


Fucky skills (cooldown, card id):

Summon (I'm just going to not change these from what they were.  Each level of summon has it's own upgrade level, it looks like, so if you want to change the summon you should be able to update that?)

If you want triggers, add them at the end.  The preferred words are "play", "attacked", or "death", but other things like OnPlay may work.

The more specific you can be, the better.  The parser will try to figure out what you mean if you're taking shortcuts, but it's not perfect.

For all/number, something is expected.  Either a number (can be 1) or "all"