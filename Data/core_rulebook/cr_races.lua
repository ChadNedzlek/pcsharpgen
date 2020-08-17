-- Converted From LST file data\pathfinder\paizo\roleplaying_game\core_rulebook\cr_races.lst
-- From repository https://github.com/pcgen/pcgen at commit 11ceb52482855f2e5f0f6c108c3dc665b12af237
SetSource({
  SourceLong="Core Rulebook",
  SourceShort="CR",
  SourceWeb="http://paizo.com/store/downloads/pathfinder/pathfinderRPG/v5748btpy88yj",
  SourceDate="2009-08",
})
DefineRace({
  Name="Dwarf",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  Size="M",
  SourcePage="p.21",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Dwarf",
      },
    },
  },
  RaceSubTypes={
    "Dwarf",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="M",
    IsPC="true",
  },
  Movement={
    Walk=20,
  },
})
DefineRace({
  Name="Elf",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  Size="M",
  SourcePage="p.22",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Elf",
      },
    },
  },
  RaceSubTypes={
    "Elf",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="M",
    IsPC="true",
  },
  Movement={
    Walk=30,
  },
})
DefineRace({
  Name="Gnome",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  Size="S",
  SourcePage="p.23",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Gnome",
      },
    },
  },
  RaceSubTypes={
    "Gnome",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="S",
    IsPC="true",
  },
  Movement={
    Walk=20,
  },
})
DefineRace({
  Name="Half-Elf",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  ServesAs={
    Race=true,
    Names={
      "Elf",
      "Human",
    },
  },
  Size="M",
  SourcePage="p.24",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Half-Elf",
      },
    },
  },
  RaceSubTypes={
    "Elf",
    "Human",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="M",
    IsPC="true",
  },
  Movement={
    Walk=30,
  },
})
DefineRace({
  Name="Half-Orc",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  ServesAs={
    Race=true,
    Names={
      "Human",
      "Orc",
    },
  },
  Size="M",
  SourcePage="p.25",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Half-Orc",
      },
    },
  },
  RaceSubTypes={
    "Orc",
    "Human",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="M",
    IsPC="true",
  },
  Movement={
    Walk=30,
  },
})
DefineRace({
  Name="Halfling",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  Size="S",
  SourcePage="p.26",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Halfling",
      },
    },
  },
  Bonuses={
    {
      Category="SAVE",
      Formula=Formula("Halfling_HalflingLuck_SaveBonus"),
      Type={
        Name="Racial",
      },
      Variables={
        "ALL",
      },
    },
  },
  RaceSubTypes={
    "Halfling",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="S",
    IsPC="true",
  },
  Movement={
    Walk=20,
  },
})
DefineRace({
  Name="Human",
  SortKey="a_base_pc",
  RaceType="Humanoid",
  Reach=5,
  Size="M",
  SourcePage="p.27",
  StartingFeats=1,
  Abilities={
    {
      Category="Internal",
      Nature="AUTOMATIC",
      Names={
        "Racial Traits ~ Human",
      },
    },
  },
  RaceSubTypes={
    "Human",
  },
  Types={
    "Humanoid",
    "Base",
    "PC",
  },
  Facts={
    BaseSize="M",
    IsPC="true",
  },
  Movement={
    Walk=30,
  },
})
DefineRace({
  Name="A test",
  SortKey="a_base_pc",
  Reach=0,
  Size="T",
  StartingFeats=1,
  StartingKitCount=1,
  StartingKitChoices={
    "A Test",
  },
  Facts={
    BaseSize="M",
  },
  Movement={
    Walk=10,
  },
})
