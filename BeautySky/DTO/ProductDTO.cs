﻿namespace BeautySky.DTO
{
    public class ProductDTO
    {

    //public int? ProductsImageId { get; set; }
        public string? ProductName { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Description { get; set; }
        public string? Ingredient { get; set; }
        public int? CategoryId { get; set; }
        public int? SkinTypeId { get; set; }
        public IFormFile? File { get; set; }
        public string? ImageDescription { get; set; }
        public bool? IsActive { get; set; }

        public DateTime? Expire { get; set; }

    }
}
