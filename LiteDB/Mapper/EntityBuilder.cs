using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiteDB
{
    /// <summary>
    /// Helper class to modify your entity mapping to document. Can be used instead attribute decorates
    /// </summary>
    public class EntityBuilder<T>
    {
        private readonly BsonMapper _mapper;
        private readonly EntityMapper _entity;

        internal EntityBuilder(BsonMapper mapper)
        {
            _mapper = mapper;
            _entity = mapper.GetEntityMapper(typeof(T), true);
        }

        /// <summary>
        /// Automatically map an entity with default settings
        /// </summary>
        /// <returns></returns>
        public EntityBuilder<T> AutoMap()
        {
            _mapper.AutoMapEntity(_entity, typeof(T));
            return this;
        }

        /// <summary>
        /// Include a property for serialization
        /// </summary>
        /// <returns></returns>
        public EntityBuilder<T> Include<K>(Expression<Func<T, K>> property)
        {
            MemberExpression memberExpression = (property.Body as MemberExpression);
            if (memberExpression == null)
            {
                throw new Exception("Illegal expression in Include() " + property.GetPath());
            }
            MemberMapper mapped = _mapper.MapMember(_entity, typeof(T),memberExpression.Member,false);
            if (_entity.Members.Any(m => m.FieldName == mapped.FieldName))
            {
                throw new Exception("Attempted to map duplicate member: " + mapped.FieldName);
            }

            _entity.Members.Add(mapped);

            return this;

        }

        /// <summary>
        /// Define which property will not be mapped to document
        /// </summary>
        public EntityBuilder<T> Ignore<K>(Expression<Func<T, K>> property)
        {
            return this.GetProperty(property, (p) =>
            {
                _entity.Members.Remove(p);
            });
        }

        /// <summary>
        /// Define a custom name for a property when mapping to document
        /// </summary>
        public EntityBuilder<T> Field<K>(Expression<Func<T, K>> property, string field)
        {
            if (field.IsNullOrWhiteSpace()) throw new ArgumentNullException("field");

            return this.GetProperty(property, (p) =>
            {
                p.FieldName = field;
            });
        }

        /// <summary>
        /// Define which property is your document id (primary key). Define if this property supports auto-id
        /// </summary>
        public EntityBuilder<T> Id<K>(Expression<Func<T, K>> property, bool autoId = true)
        {
            return this.GetProperty(property, (p) =>
            {
                p.FieldName = "_id";
                p.AutoId = autoId;
            });
        }

        /// <summary>
        /// Define an index based in a property on entity
        /// </summary>
        public EntityBuilder<T> Index<K>(Expression<Func<T, K>> property, bool unique = false)
        {
            return this.GetProperty(property, (p) =>
            {
                p.IsUnique = unique;
            });
        }

        /// <summary>
        /// Define an index based in virtual property (getter function)
        /// </summary>
        public EntityBuilder<T> Index<K>(string indexName, Func<T, BsonValue> getter, bool unique = false)
        {
            if (indexName.IsNullOrWhiteSpace()) throw new ArgumentNullException("indexName");

            _entity.Members.Add(new MemberMapper
            {
                FieldName = indexName,
                MemberName = indexName,
                Getter = x => (object)getter((T)x),
                Setter = null,
                DataType = typeof(BsonValue),
                IsUnique = unique
            });

            return this;
        }

        /// <summary>
        /// Define an index based in a field name on BsonDocument
        /// </summary>
        public EntityBuilder<T> Index(string field, bool unique = false)
        {
            if (field.IsNullOrWhiteSpace()) throw new ArgumentNullException("field");

            var p = _entity.Members.FirstOrDefault(x => x.FieldName == field);

            if (p == null) throw new ArgumentException("field not found");

            p.IsUnique = unique;

            return this;
        }

        /// <summary>
        /// Define a subdocument (or a list of) as a reference
        /// </summary>
        public EntityBuilder<T> DbRef<K>(Expression<Func<T, K>> property, string collection = null)
        {
            return this.GetProperty(property, (p) =>
            {
                BsonMapper.RegisterDbRef(_mapper, p, collection ?? _mapper.ResolveCollectionName(typeof(K)));
            });
        }

        /// <summary>
        /// Get a property based on a expression. Eg.: 'x => x.UserId' return string "UserId"
        /// </summary>
        private EntityBuilder<T> GetProperty<K>(Expression<Func<T, K>> property, Action<MemberMapper> action)
        {
            if (property == null) throw new ArgumentNullException("property");

            var prop = _entity.GetMember(property);

            if (prop == null)
            {
                Include(property);
                prop = _entity.GetMember(property);
            }

            action(prop);

            return this;
        }
    }
}