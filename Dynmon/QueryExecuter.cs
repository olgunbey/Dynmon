using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Dynmon.Enums;
using static Dynmon.Exceptions;

namespace Dynmon
{
    public class QueryExecuter
    {
        public async Task<JsonNode?> ExecuteQueryAsync(MongoQueryRequest mongoQueryRequest, IMongoCollection<BsonDocument> data)
        {
            List<BsonDocument> lookupPipeline;
            if (mongoQueryRequest.Lookups == null)
            {
                lookupPipeline = await HandleFiltersAndProjection(mongoQueryRequest.Filters, mongoQueryRequest.SelectedFields, data);
            }
            else
            {
                lookupPipeline = await HandleLookupsAndProjection(mongoQueryRequest.Filters, mongoQueryRequest.Lookups, mongoQueryRequest.SelectedFields, data);
            }

            AddUnwindsIfNecessary(mongoQueryRequest.Lookups, lookupPipeline);

            var jsonData = await data.Aggregate<BsonDocument>(lookupPipeline).ToListAsync();
            return JsonNode.Parse(jsonData.ToJson());
        }
        public async Task<JsonNode?> ExecuteQueryAsync(MongoQueryRequest mongoQueryRequest, string connectionString, string cluster, string collection)
        {
            MongoClient mongoClient = new MongoClient(connectionString);
            IMongoCollection<BsonDocument> getCollection = mongoClient.GetDatabase(cluster).GetCollection<BsonDocument>(collection);

            List<BsonDocument> lookupPipeline;
            if (mongoQueryRequest.Lookups == null)
            {
                lookupPipeline = await HandleFiltersAndProjection(mongoQueryRequest.Filters, mongoQueryRequest.SelectedFields, getCollection);
            }
            else
            {
                lookupPipeline = await HandleLookupsAndProjection(mongoQueryRequest.Filters, mongoQueryRequest.Lookups, mongoQueryRequest.SelectedFields, getCollection);
            }
            AddUnwindsIfNecessary(mongoQueryRequest.Lookups, lookupPipeline);

            var jsonData = await getCollection.Aggregate<BsonDocument>(lookupPipeline).ToListAsync();
            return JsonNode.Parse(jsonData.ToJson());

        }
        public async Task<bool> InsertAsync(Insert insert)
        {
            BsonDocument filter = new();
            if (insert.Filter is { })
            {
                filter = BuildFilterCriteria(insert.Filter);
            }

            BsonDocument bsonDocument = new();
            JsonNode? insertJsonData = JsonNode.Parse(insert.JsonData);


            var existsUser = insert.Data.Find(filter).FirstOrDefault();

            if (insertJsonData is JsonArray && insert.InsertProperty is not null)
            {
                var jsonArray = JsonArray.Parse(insert.JsonData);

                BsonArray bsonArray = new();
                foreach (var item in jsonArray.AsArray())
                {
                    // JSON öğesini BSON öğesine dönüştür
                    bsonArray.Add(BsonDocument.Parse(JsonSerializer.Serialize(item)));
                }

                // Bu öğeleri tek tek array'e eklemek için $push kullanılır
                BsonDocument update = new();
                update.Add("$push", new BsonDocument(insert.InsertProperty, new BsonDocument
                {
                    { "$each", bsonArray }
                }));

                // Filtre ile eşleşen belgeyi güncelle
                var result = await insert.Data.UpdateOneAsync(filter, update);

                return result.ModifiedCount > 0;
            }
            else
            {
                bsonDocument = BsonDocument.Parse(insert.JsonData);
            }

            try
            {
                await insert.Data.InsertOneAsync(bsonDocument);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<List<BsonDocument>> HandleFiltersAndProjection(IEnumerable<Filter>? filters, List<string> selectedFields, IMongoCollection<BsonDocument> data)
        {
            var lookupPipeline = new List<BsonDocument>();
            if (filters != null && filters.Any())
            {
                var match = BuildFilterCriteria(filters);
                lookupPipeline.Add(new BsonDocument("$match", match));
            }
            var selectProject = new BsonDocument("$project", new BsonDocument());
            await AddSelectedFieldsAsync(selectProject, selectedFields, data);
            lookupPipeline.Add(selectProject);
            return lookupPipeline;
        }

        private async Task<List<BsonDocument>> HandleLookupsAndProjection(IEnumerable<Filter>? filters, List<Lookup> lookups, List<string> selectedFields, IMongoCollection<BsonDocument> dataCollection)
        {
            List<BsonDocument> lookupPipeline = [.. await BuildLookupPipeline(filters, lookups)];
            var selectProject = lookupPipeline.FirstOrDefault(y => y.Names.Contains("$project"));
            if (selectProject != null)
            {
                await AddSelectedFieldsAsync(selectProject, selectedFields, dataCollection);
            }
            return lookupPipeline;
        }

        private void AddUnwindsIfNecessary(List<Lookup>? lookups, List<BsonDocument> lookupPipeline)
        {
            if (lookups != null)
            {
                var unwinds = BuildUnwindStages(lookups);
                if (unwinds.Any())
                {
                    lookupPipeline.AddRange(unwinds);
                }
            }
        }

        private async Task AddSelectedFieldsAsync(BsonDocument currentProjection, List<string> selectedFields, IMongoCollection<BsonDocument> dataCollection)
        {
            var projects = currentProjection["$project"].AsBsonDocument;
            if (selectedFields != null && selectedFields.Any())
            {
                if (selectedFields.Contains("_id"))
                {
                    projects["_id"] = 1;
                    selectedFields.Remove("_id");
                }
                foreach (var field in selectedFields)
                {
                    projects[field] = 1;
                }
            }
            else
            {
                var firstDoc = await dataCollection.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync();
                if (firstDoc != null)
                {
                    foreach (var field in firstDoc.Names)
                    {
                        if (field != "_id")
                        {
                            projects[field] = 1;
                        }
                    }
                }
            }
        }

        private async Task<BsonDocument> AddSelectedFieldsAsync(string alias, string rootPath, List<string>? lookupSelects)
        {
            BsonDocument currentProjection = new();
            if (lookupSelects != null && lookupSelects.Any())
            {
                foreach (var lookupSelect in lookupSelects)
                {
                    string fieldPath = string.IsNullOrEmpty(rootPath) ? alias : $"{rootPath}.{alias}";
                    currentProjection.Add(
                    $"{(string.IsNullOrEmpty(rootPath) ? alias : rootPath + "." + alias)}.{lookupSelect}",
                    1);

                }
            }
            else
            {
                currentProjection.Add(string.IsNullOrEmpty(rootPath) ? alias : rootPath + '.' + alias, 1);
            }
            return currentProjection;
        }

        private BsonDocument BuildFilterExpression(Filter filter)
        {
            BsonValue bsonValue;
            if (filter.Value is JsonElement jsonElement)
                bsonValue = ConvertToBsonValue(jsonElement);

            bsonValue = ConvertToBsonValue(filter.Value);

            var data = filter.FilterOperator switch
            {
                FilterOperators.Equals => new BsonDocument("$eq", bsonValue),
                FilterOperators.NotEquals => new BsonDocument("$ne", bsonValue),
                FilterOperators.Contains => new BsonDocument("$regex", new BsonRegularExpression(bsonValue.ToString(), "i")),
                FilterOperators.GreaterThan => new BsonDocument("$gt", bsonValue),
                FilterOperators.LessThan => new BsonDocument("$lt", bsonValue),
                FilterOperators.LessThanOrEqual => new BsonDocument("$lte", bsonValue),
                FilterOperators.GreaterThanOrEqual => new BsonDocument("$gte", bsonValue),
                FilterOperators.In => new BsonDocument("$in", bsonValue),
                FilterOperators.NotIn => new BsonDocument("$nin", bsonValue),
                _ => throw new FilterOperatorException("Invalid filter operator")
            };
            return new BsonDocument(filter.Field, data);
        }

        private BsonValue ConvertToBsonValue(object values)
        {
            return values switch
            {
                Int32 v => new BsonInt32(v),
                Int64 v => new BsonInt64(v),
                string v => new BsonString(v),
                DateTime v => new BsonDateTime(v),
                ObjectId v => new BsonObjectId(v),
                bool v => new BsonBoolean(v),
                decimal v => new BsonDecimal128(v),
                double v => new BsonDouble(v),
                _ => ConvertToBsonArray(values)
            };

        }

        private BsonValue ConvertToBsonValue(JsonElement values)
        {
            return values.ValueKind switch
            {
                JsonValueKind.False => new BsonBoolean(false),
                JsonValueKind.True => new BsonBoolean(true),
                JsonValueKind.Number => values.TryGetInt32(out int valueInt32) ? new BsonInt32(valueInt32) :
                                        values.TryGetInt64(out long valueInt64) ? new BsonInt64(valueInt64) :
                                        values.TryGetDouble(out var valueDouble) ? new BsonDouble(valueDouble) :
                                        new BsonDecimal128(values.GetDecimal()),
                JsonValueKind.String => new BsonString(values.GetString()),
                JsonValueKind.Object => new BsonObjectId(ObjectId.Parse(values.EnumerateObject().SingleOrDefault(y => y.Name == "$oid").Value.ToString())),
                JsonValueKind.Undefined => BsonUndefined.Create("undefined"),
                JsonValueKind.Null => BsonNull.Create("null"),
                _ => ConvertToBsonArray(values)
            };
        }

        private BsonValue ConvertToBsonArray(object values) => values switch
        {
            IEnumerable<ObjectId> v => new BsonArray(v.Select(id => new BsonObjectId(id))),
            IEnumerable<string> v => new BsonArray(v.Select(data => new BsonString(data))),
            IEnumerable<Int32> v => new BsonArray(v.Select(data => new BsonInt32(data))),
            IEnumerable<Int64> v => new BsonArray(v.Select(data => new BsonInt64(data))),
            IEnumerable<DateTime> v => new BsonArray(v.Select(datetime => new BsonDateTime(datetime))),
            BsonArray v => new BsonArray(v.Select(x => x)),
            IEnumerable<decimal> v => new BsonArray(v.Select(decimal_ => new BsonDecimal128(decimal_))),
            IEnumerable<double> v => new BsonArray(v.Select(double_ => new BsonDouble(double_))),
            _ => throw new NotImplementedException("BsonArray type not matched")
        };

        private async Task<List<BsonDocument>> BuildLookupPipeline(IEnumerable<Filter>? filters, IEnumerable<Lookup> lookups)
        {
            var aggregatePipeline = new List<BsonDocument>();
            var projectFields = new BsonDocument();

            foreach (var lookup in lookups)
            {
                var localFieldParts = lookup.LocalField.Split('.');
                lookup.RootPath = CalculateRootPath(localFieldParts);

                if (string.IsNullOrEmpty(lookup.RootPath))
                {
                    var lookupQuery = PrepareLookupStage(lookup);
                    aggregatePipeline.Add(lookupQuery);
                }
                else
                {
                    var pipelineArray = new BsonArray(aggregatePipeline);
                    BsonDocument? rootStage = pipelineArray.FirstOrDefault(y => y["$lookup"].AsBsonDocument["from"] == localFieldParts[0])?.AsBsonDocument;

                    var remainingSubQuery = localFieldParts.ToList();
                    if (rootStage == null)
                        throw new LookupFromNotFoundException("lookup from not found");

                    TraversePipelineForLookups(remainingSubQuery, rootStage, lookup);
                }

                var fields = await AddSelectedFieldsAsync(lookup.As, lookup.RootPath, lookup.Selects);
                foreach (var element in fields.Elements)
                {
                    projectFields.Add(element.Name, element.Value);
                }
            }
            if (projectFields.Names.Any())
                aggregatePipeline.Add(new BsonDocument("$project", projectFields));

            return aggregatePipeline;
        }

        private string CalculateRootPath(string[] parts)
        {
            if (parts.Length <= 1)
                return string.Empty;

            return string.Join(".", parts.Take(parts.Length - 1));
        }

        private void TraversePipelineForLookups(List<string> splitSubQueryPaths, BsonDocument rootStage, Lookup lookup)
        {
            BsonDocument? currentStage = null;

            if (splitSubQueryPaths.Count == 0)
            {
                var newLookupStage = PrepareLookupStage(lookup);
                rootStage["$lookup"]["pipeline"].AsBsonArray.Add(newLookupStage);
                return;
            }

            for (int i = 0; i < splitSubQueryPaths.Count; i++)
            {
                splitSubQueryPaths.Remove(splitSubQueryPaths[i]);

                var lookupStages = rootStage["$lookup"]["pipeline"].AsBsonArray
                    .Where(y => y.AsBsonDocument.Names.Contains("$lookup"))
                    .ToList();

                if (lookupStages.Count == 0)
                {
                    var newLookupStage = PrepareLookupStage(lookup);
                    rootStage["$lookup"]["pipeline"].AsBsonArray.Add(newLookupStage);
                    return;
                }
                else
                {
                    currentStage = lookupStages.FirstOrDefault(y => y["$lookup"]["from"] == splitSubQueryPaths[i])?.AsBsonDocument;
                }

                if (currentStage == null) continue;

                TraversePipelineForLookups(splitSubQueryPaths, currentStage, lookup);
            }
        }

        private BsonDocument PrepareLookupStage(Lookup lookup)
        {
            var localField = lookup.LocalField.Split('.').Last();
            var letLocalField = char.ToLower(localField[0]) + localField.Substring(1);

            BsonDocument expressionInMatch = BuildLookupMatch(lookup);
            BsonDocument match = new BsonDocument();
            if (lookup.Filters != null && lookup.Filters.Any())
                match = BuildFilterCriteria(lookup.Filters);


            if (match.Values.Count() != 0)
                expressionInMatch["$match"].AsBsonDocument.AddRange(match);

            return new BsonDocument("$lookup", new BsonDocument
            {
                {"from",lookup.From },
                {"let",new BsonDocument(letLocalField,"$"+localField) },
                {"pipeline",new BsonArray
                    {
                    expressionInMatch
                    }
                },
                {"as",lookup.As }
            });
        }

        private BsonDocument BuildLookupMatch(Lookup lookup)
        {
            string key = "$" + lookup.ForeignKey;
            var localField = lookup.LocalField.Split('.').Last();
            string value = "$$" + (char.ToLower(localField[0]) + localField.Substring(1));

            var relationCondition = lookup.RelationType switch
            {
                RelationTypes.One => new BsonDocument("$eq", new BsonArray { key, value }),
                RelationTypes.Many => new BsonDocument("$in", new BsonArray { key, value }),
                _ => throw new NotImplementedException()
            };
            return new BsonDocument("$match", new BsonDocument("$expr", relationCondition));
        }

        private IEnumerable<BsonDocument> BuildUnwindStages(IEnumerable<Lookup> lookups)
        {
            var groupBylookups = lookups.GroupBy(y => y.RelationType);

            return groupBylookups.SelectMany(group =>
             group.Select(lookup =>
             new BsonDocument("$unwind", new BsonDocument
             {
                { "path", "$" + (string.IsNullOrEmpty(lookup.RootPath) ? lookup.From : $"{lookup.RootPath}.{lookup.From}") },
                { "preserveNullAndEmptyArrays", lookup.PreserveNullAndEmptyArrays }
             }))
             );

        }

        private BsonDocument BuildFilterCriteria(IEnumerable<Filter> filters)
        {
            BsonDocument filterCriteria = [];

            if (filters.Any(y => y.MatchOperator == MatchOperators.None))
                filterCriteria = BuildFilterExpression(filters.First());

            var groupedFilters = filters
                .Where(y => y.MatchOperator != MatchOperators.None)
                .GroupBy(y => y.MatchOperator);

            foreach (var group in groupedFilters)
            {
                if (group.Any())
                {
                    string operator_ = group.Key == MatchOperators.And ? "$and" : "$or";
                    filterCriteria.Add(operator_, new BsonArray(group.Select(BuildFilterExpression)));
                }
            }

            return filterCriteria;
        }
    }
}
