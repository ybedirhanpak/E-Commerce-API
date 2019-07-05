﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

//Imports
using e_commerce_api.Models;
using System.Collections.Generic;
using System.Linq;

//MongoDB
using MongoDB.Driver;

namespace e_commerce_api.Services
{
    public class ProductService
    {
        //Create a collection object to refer products
        private readonly IMongoCollection<Product> _products;

        public ProductService(IECommerceDatabaseSettings settings)
        {
            //Create a mongo client instance to connect mongo
            var client = new MongoClient(settings.ConnectionString);
            //Create the database which is "e-commerce"
            var database = client.GetDatabase(settings.DatabaseName);

            //Initialize collection by getting from mongodb
            _products = database.GetCollection<Product>(settings.ProductsCollectionName);
        }

        /**
         *  Gets all products
         */
        public List<Product> Get() =>
            _products.Find(p => true).ToList();

        /**
         * Gets the product with given id
         */
        public Product Get(string id) =>
            _products.Find<Product>(p => p.Id == id).FirstOrDefault();

        /**
         * Loads given product into the collection
         */
        public Product Create(Product product)
        {
            _products.InsertOne(product);
            return product;
        }

        /**
         * Updates the product that has the given id with new product
         */
        public void Update(string id, Product product) =>
            _products.ReplaceOne<Product>(p => p.Id == id, product);

        /**
         * Removes given product from the collection
         */
        public void Remove(Product product) =>
            _products.DeleteOne(p => p.Id == product.Id);

        /**
         * Removes the product with its id
         */
        public void Remove(string id) =>
            _products.DeleteOne(p => p.Id == id);
    }
}
