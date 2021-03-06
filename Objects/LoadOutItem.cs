﻿namespace Pluton.Rust.Objects
{
	using Core;

    public class LoadOutItem : CountedInstance
    {
        public readonly int Amount;
        public readonly string Name;

        public LoadOutItem(string name)
        {
            Amount = 1;
            Name = name;
        }

        public LoadOutItem(string name, int amount)
        {
            Amount = amount;
            Name = name;
        }

		public InvItem invItem => new InvItem(Name, Amount);
    }
}

