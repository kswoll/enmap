﻿using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IEntityFetcherItem : IFetcherItem
    {
        Type SourceType { get; set; }
        Type DestinationType { get; set; }
//        Mapper Mapper { get; }                   // The mapper registered for the entity type that contains the primary key of the PrimaryEntityType
    }
}