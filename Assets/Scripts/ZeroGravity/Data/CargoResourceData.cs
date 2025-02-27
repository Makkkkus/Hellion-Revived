using System;
using System.Collections.Generic;
using ProtoBuf;

namespace ZeroGravity.Data
{
	[Serializable]
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class CargoResourceData : ISceneData
	{
		public ResourceType ResourceType;

		public float Quantity;

		public ResourcesSpawnSettings[] SpawnSettings;

		public static Dictionary<ResourceType, float> ListToDictionary(List<CargoResourceData> list)
		{
			Dictionary<ResourceType, float> dictionary = new Dictionary<ResourceType, float>();
			if (list != null)
			{
				foreach (CargoResourceData item in list)
				{
					dictionary[item.ResourceType] = item.Quantity;
				}

				return dictionary;
			}

			return dictionary;
		}

		public static List<CargoResourceData> DictionaryToList(Dictionary<ResourceType, float> dictionary)
		{
			List<CargoResourceData> list = new List<CargoResourceData>();
			if (dictionary != null)
			{
				foreach (KeyValuePair<ResourceType, float> item in dictionary)
				{
					list.Add(new CargoResourceData
					{
						ResourceType = item.Key,
						Quantity = item.Value
					});
				}

				return list;
			}

			return list;
		}
	}
}
