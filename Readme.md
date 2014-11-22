The purpose of enmap is to provide a mapping framework that allows for easy composibility between entity types and flexible 
translation of database properties into domain properties.  In particular there are several key areas that are difficult to 
implement with other mapping frameworks and/or awkward to implement without a mapping framework in the first place:

* Translating database values to model values with a step that requires unencumbered leveraging of normal C# syntax without 
having to worry about expression tree limitations.
* Batching up relationships into separate queries.  Unfortunately, when you project container properties into your entity 
framework projections, the default implementation forces an implementation that only requires one SQL query.  While this 
notion is admirable in the abstract, in practice it produces queries with an _enormous_ amount of wasted replication.  If 
a `person` has 10 addresses, then each row in the result set will include all the properties of `person` and all the properties
of `address`.  This produces gigantic queries that are both difficult to parse and more expensive to ultimately run.

Getting Started
-------

All mappers must be contained in a _registry_.  There are two ways to do this:

* Subclass `MapperRegistry` and override `Register`
* Instantiate `MapperRegistry` and pass to the constructor a delegate `Action<MapperRegistry<TContext>>` 

Either way you are responsible for registering new mappers using this protocol.  For simplicity the following examples will
assume you are subclassing and overriding `Register`, but everything that follows applies to either technique.  To register 
a mapper without defining any properties, you'd use:

    Map<TEntityFrameworkType, TDomainType>();

But unlike some mappers such as Automapper, we deliberately do not offer any conventinon-based implementations.  Enmap -- by
default -- requires you to specify each translation explicitly.  This is because it's a better long-term solution as it 
forces you to declare each translation in a way that can be caught by refactoring tools.  If your rename one property or another,
proper refactoring tools will take care of this for you; but if you try to refactor by hand in these scenarios, you'll get 
compiler errors in these mapping definitions.  

Therefore, to map _any_ property, you have to _explicitly_ define the mapping.  Say you want to say the `Id` property of the 
entity type maps to the `Id` property of the domain type.  To do so, you _must_ declare the translation:

    Map<TEntityFrameworkType, TDomainType>()
        .For(x => x.Id).From(x => x.Id);

Here we define a simple mapping translation that copies the value `Id` from the database type to the property `Id` of the domain 
type.  These translations are extremely typical.  And if this were the only sort of translation offered by the mapping framework
it would not be terribly useful.   

The key aspect in which a framework such as this is useful is that it can handle sub-relationship in a simple and concise way that
_encourages_ you to define mappings using this framework, rather than making you feel this is just a chore.  Aside from the 
aforementioned simple mapping, there are five other types:

* Inline single entity relationships (i.e. a FK to another table)
* Inline container entity relationships (you have a collection property representing a relationship)
* Fetch single entity relationships, meaning that a separate query will be run to pull all the entities of that particular type
that were referenced from the containing query.
* Fetch container entity relationships, with the same separate-query meaning as defined above.
* Ad-hoc fetching of batches of objects of a particular type based on an id.  This is similar to the fetch based techniques outlined 
above except may apply to getting objects from any data source at all.

By default, a fetch based approach is used for containers, since inlining container relationships usually produces wildly 
inefficient queries, since all the columns from the parent must be duplicated for each row of the child.  Similarly, single
entity relationships are by default mapped using the inline technique, since reducing the number of queries is beneficial
and inlining the relationship usually has little extra cost.

Finally, let's look at some examples of each of those techniques.

Inline Single Entity Relationships
-----

For an inline single entity relationship, consider a `DbPerson` entity with a reference to a `DbAddress` entity:

    public class DbPerson 
    {
        public int Id { get; set; }
        public int AddressId { get; set; }
        public DbAddress Address { get; set; }
    }

    public class DbAddress 
    {
        public int Id { get; set; }
        public string Street { get; set; }
    }

And with corresponding model types:

    public class Person
    {
        public int Id { get; set; }
        public Address Address { get; set; }
    }

    public class Address 
    {
        public int Id { get; set; }
        public string Street { get; set; }
    }

The mapping for this would look like:

    Map<DbPerson, Person>()
        .For(x => x.Id).From(x => x.Id)
        .For(x => x.Address).From(x => x.Address);
    Map<DbAddress, Address>()
        .For(x => x.Id).From(x => x.Id)
        .For(x => x.Street).From(x => x.Street);

The actual Entity Framework projection might look a bit like:

    dbContext.Persons.Select(x => new Person 
    {
        Id = x.Id,
        Address = new Address 
        {
            Id = x.Address.Id,
            Street = x.Address.Street
        }
    });

Importantly, the mapping for `Address` is *composable* in a way the raw Entity Framework projection is not.  Without dynamically
generating these expression trees, it's not possible to reuse projections for relationships like `Address`.

Fetch Based Single Entity Relationship
-----

To continue with our previous example, let's change the mapping for `DbPerson` to:

    Map<DbPerson, Person>()
        .For(x => x.Id).From(x => x.Id)
        .For(x => x.Address).From(x => x.Address).Fetch();

Since the default behavior is inline for this type of relationship, we need to explicitly indicate we want to use the 
fetch-based behavior.  Doing this changes the projection above to something like:

    var persons = dbContext.Persons.Select(x => new Person 
    {
        Id = x.Id,
        AddressId = x.AddressId
    });
    var addresses = dbContext.Addresses.Select(x => new Address
    {
        Id = x.Id,
        Street = x.Street
    });

Without going into too much detail, the addresses are interleaved back into the `Person` model type after both queries
have been executed.  There are a variety of situations -- particularly with especially complicated mappings -- where
using a fetch based approach (and therefore multiple queries) will actually improve performance.

Inline and Fetch Based Container Relationships
-----

The two container-based approaches are very similar to the single entity approaches.  However, as mentioned earlier,
inlining container relationships is almost always very inefficient.  Therefore, the default is to use a fetch-based
approach.  To force it to use the inline behavior, you must override the mapping with `.Inline()` in an analogous
way that you had forced it to use `.Fetch()` for the single entity scenario.

Batch Based Behavior
-----

Sometimes you may have an id in your tables that represent an entity or object that does not actually exist in the 
database.  Perhaps it's a key to a key/value store and all you have in the database is a GUID string.  To facilitate
these scenarios, you can provide your own batch processing that works in a similar fashion to the fetch-based approaches
outlined above.  To accomplish this, you must create a class that is responsible for fetching the external data.  This 
should be an implementation of `IBatchProcessor` and implement its one method:

    Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context);

The `items` parameter contains a sequence of `IBatchFetcherItem`, which is just a contract for an object that specifies:

1. The object implementing `IBatchProcessor`.  (i.e. the class you are in the process of implementing)
2. The id representing the object you are trying to fetch (the value in the key/value store, say)
3. A callback named `ApplyFetchedValue` that is invoked when the fetched item has been obtained.

So to implement this method, you would grab all the entity ids (`items.Select(x => x.EntityId)`) and make a call 
to your key/value store to obtain the values for all those keys.  Then for each value returned, you need to find the
corresponding `IBatchFetcherItem` and call its `ApplyFetchedValue` method with the value.

Once you have set up the your batch processor, you can use it in your mapping configurations.  For example, suppose you have
an entity such as:

    public class DbNotification
    {
        public int Id { get; set; }
        public string PostId { get; set; }
    }

And model types such as:

    public class Notification 
    {
        public int Id { get; set; }
        public Post Post { get; set; }
    }

    public class Post
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

To set up a mapper for this using your batch processor, you'd have something like:

    var batchProcessor = new PostsBatchProcessor();
    Map<DbNotification, Notification>()
        .For(x => x.Id).From(x => x.Id)
        .For(x => x.Post).Batch(batchProcessor).Collect(x => x.PostId);

This will set up a mapper that collects all the `PostId`s and then, via your implementation of `IBatchProcessor`,
fetches all the posts at once from the key/value store and subsequently interleaves the results into the `Post` property
of `Notification`.

Adhoc Translations of Database Values to Model Values
-----

One of the most common problems with other mappers is they don't provide any straightforward way to further transform the 
database value into the type expected in the model.  For example, you might store a `TimeSpan` value in the database as
an int that represents seconds.  However, your model type might choose to surface this value as a `TimeSpan`.  If you want 
to do this, you need to ensure that the `TimeSpan` transformation only happens _after_ the projected value has been resolved.
With Enmap, this is trivial.  Supposing you have the following types:

    public class DbJob
    {
        public int Id { get; set; }
        public int Period { get; set; }
    }

    public class Job 
    {
        public int Id { get; set; }
        public TimeSpan Period { get; set; }
    }

Here it's not straightforward to build a SQL projection that honors this mapping.  For example, this is illegal:

    var jobs = db.Jobs.Select(x => new Job 
    {
        Id = x.Id,
        Period = TimeSpan.FromSeconds(x.Period)
    });

The reason is that `TimeSpan.FromSeconds` is not "translatable to SQL".  However, all you _really_ want from the 
database is the `Period` in seconds.  Once the query is complete, you just want to perform a translation that converts
the int value in seconds to a `TimeSpan` value.  With Enmap, you simply need to use the `.To(...)` operator.  The
contents of the function passed to `.To` is executed _after_ the query has been executed -- meaning no SQL translation 
errors will happen.  Here's an example of what that mapping might look like:

    Map<DbJob, Job>()
        .For(x => x.Id).From(x => x.Id)
        .For(x => x.Period).From(x => x.Period).To(x => TimeSpan.FromSeconds(x));

The `.From(...)` clause is responsible for managing the SQL projection, and thus must be fairly simple, such as the 
aforementioned property reference.  In contrast, the `.To(...)` method is run in a normal C# context, and thus calling
`TimeSpan.FromSeconds(...)` is perfectly valid.        

The final result is that you get to define all these translations in a clean way that fully encapsulates various translations
at the precise place where the mapping is declared in the first place.