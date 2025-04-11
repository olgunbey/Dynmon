<h1 align="left">Dynmon</h1>

**Dynmon** is a package for managing dynamic lookup processes in Mongo.


# Packages
[![Dynmon on NuGet](https://img.shields.io/nuget/v/Dynmon?label=Dynmon)](https://www.nuget.org/packages/Dynmon)

## Parameters


### Filters
Defines the valid filters for the query. Each filter specifies a particular condition on a particular field.

- **Field**: Specifies the field to be filtered.
- **Value**: Defines the value to be searched for in the specified field.
- **FilterOperator**: Determines how the filtering process is performed.
- **MatchOperator**: Determines how multiple filter conditions are combined.

### Lookups
Defines the relational data to be included in the query. Each lookup is used to retrieve data from a different collection.

- **From**: Specifies the collection from which the query will retrieve data.
- **RelationType**: Specifies the type of relational data. Includes the Unwind structure.
- **As**: Specifies the name of the relational data to be returned as a result of the query.
- **ForeignKey**: Specifies the key field in the related collection.
- **LocalField**: Specifies the field in the queried collection.
- **Selected**: Specifies the fields to return from the associated collection.
- **PreserveNullAndEmptyArrays**: Specifies how to return results when associated data is missing.

### SelectedFields
Specifies the fields in the main table to be returned.

### Data
Additional data to be used for the query. This data can be used to specify the conditions of the query.

# Examples Usage

## General Usage

```csharp
QueryExecuter queryExecuter = new();
var response=await queryExecuter.ExecuteQueryAsync(filters: null,
    lookups: new List<Lookup>()
    {
        new Lookup()
        {
            From="District",
            As="District",
            ForeignKey="_id",
            LocalField="DistrictId",
            RelationType=RelationType.Many,
            PreserveNullAndEmptyArrays=true,
            Selects=new List<string>{"Name","IsDeleted"},
            Filters = new List<Filter>()
            {
                new Filter()
                {
                    Field="IsDeleted",
                    MatchOperator=MatchOperator.None,
                    FilterOperator=FilterOperator.Equals,
                    Value=false
                }
            }
        }
    },
    selectedFields: new List<string> { "_id" },
    data: data);
```
## Using PreserveNullAndEmptyArrays
```csharp
QueryExecuter queryExecuter = new();
JsonNode? response = await queryExecuter.ExecuteQueryAsync(filters: null,
    lookups: new List<Lookup>()
    {
        new Lookup()
        {
            From="SymptomCategory",
            As="SymptomCategory",
            ForeignKey="_id",
            LocalField="SymptomCategoryId",
            RelationType=Enums.RelationTypes.One,
            Selects= new List<string>{"Name","IsDeleted"},
            PreserveNullAndEmptyArrays=false,
            Filters = new List<Filter>()
            {
                new Filter()
                {
                    Field="IsDeleted",
                    MatchOperator=Enums.MatchOperators.None,
                    FilterOperator=Enums.FilterOperators.Equals,
                    Value=false
                }
            }
        }
    },
    selectedFields: new List<string> { "_id", "Name" }, getAllCollections);
```
- If PreserveNullAndEmptyArrays is false, the SymptomCategory collection will return this data even if it is null.
- If PreserveNullAndEmptyArrays is true, the SymptomCategory collection does not return this data.

## Nested Query 

```csharp
QueryExecuter queryExecuter = new();
JsonNode? response = await queryExecuter.ExecuteQueryAsync(filters: null,
    lookups: new List<Lookup>()
    {
        new Lookup()
        {
            From="ModelGroup",
            As="ModelGroup",
            ForeignKey="_id",
            LocalField="ModelGroupId",
            RelationType=Enums.RelationTypes.One,
            Selects= new List<string>{"Name","IsDeleted"},
        },
        new Lookup()
        {
            From="DeviceType",
            As="DeviceType",
            ForeignKey="_id",
            LocalField="ModelGroup.DeviceTypeId",
            RelationType = Enums.RelationTypes.One,
            Selects= new List<string>{"Name","IsDeleted"},
        }
    },
    selectedFields: new List<string> { "_id", "Name" }, getAllCollections);
```
- For nested lookups, **LocalField** is given the address of the lookup. For example, the DeviceType collection above.

