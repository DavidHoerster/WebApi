---
title : "Referential Constraint support for Web API OData V3 & V4"
layout: post
category: Edm Model Builder
---

The following sample codes can be used for Web API OData V3 & V4 with a little bit function name changing.

#### Let's have a sample model

{% highlight csharp %}

public class Customer
{
    public int Id { get; set; }
       
    public IList<Order> Orders { get; set; }
}

public class Order
{
    public int OrderId { get; set; }
 
    public int CustomerId { get; set; }         

    public Customer Customer { get; set; }
}

{% endhighlight %}

#### Define Referential Constraint Explicitly

{% highlight csharp %}

ODataModelBuilder builder = new ODataModelBuilder();
builder.EntityType<Customer>().HasKey(c => c.Id).HasMany(c => c.Orders);
builder.EntityType<Order>().HasKey(c => c.OrderId)
    .HasRequired(o => o.Customer, (o, c) => o.CustomerId == c.Id);

{% endhighlight %}

#### Define Referential Constraint Implicitly

##### Using Attribute

There is an attribute named “ForeignKeyAttribute” which can be place on:

1. the foreign key property and specify the associated navigation property name, for example: 

{% highlight csharp %}
public class Order
{
    public int OrderId { get; set; }

    [ForeignKey("Customer")]
    public int MyCustomerId { get; set; }

    public Customer Customer { get; set; }
}

{% endhighlight %}


2. a navigation property and specify the associated foreign key name, for example:

{% highlight csharp %}
public class Order
{
    public int OrderId { get; set; }

    public int CustId1 { get; set; }
    public string CustId2 { get; set; }

    [ForeignKey("CustId1,CustId2")]
    public Customer Customer { get; set; }
}

{% endhighlight %}
Where, Customer has two keys.

##### Using Convention

If users don’t add any referential constraint, Web API will try to help users to discovery the foreign key automatically. There are two conventions as follows:
1. With same property type and same type name plus key name. For example:
   
{% highlight csharp %}
public class Customer
{ 
   [Key]
   public string Id {get;set;}
   public IList<Order> Orders {get;set;}
}

public class Order
{
    public int OrderId { get; set; }
    public string CustomerId {get;set;}
    public Customer Customer { get; set; }
}

{% endhighlight %}
*Where*, *Customer* type name "Customer" plus key name "Id" equals the property "CustomerId" in the *Order*.

2. With same property type and same property name. For example:
   
{% highlight csharp %}
public class Customer
{ 
   [Key]
   public string CustomerId {get;set;}
   public IList<Order> Orders {get;set;}
}

public class Order
{
    public int OrderId { get; set; }
    public string CustomerId {get;set;}
    public Customer Customer { get; set; }
}

{% endhighlight %}

*Where*, Property (key) "CustomerId" in the *Customer* equals the property "CustomerId" in the *Order*.

##### Build Edm Model
It's normally to build the Edm Model implicitly.

{% highlight csharp %}
public IEdmModel GetEdmModel()
{            
    ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
    builder.EntitySet<Customer>("Customers");
    builder.EntitySet<Order>("Orders");
    return builder.GetEdmModel();
}

{% endhighlight %}