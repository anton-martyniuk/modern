﻿using Modern.LiteDb.Examples.Entities;
using Modern.Repositories.Abstractions.Specifications;

namespace Modern.LiteDb.Examples.Specifications;

public class WonderfulCarsSpecification : Specification<CarDbo>
{
    public WonderfulCarsSpecification()
    {
        AddFilteringQuery(dbo => dbo.Manufacturer == "Mazda");
        AddOrderByQuery(dbo => dbo.Price);
    }
}