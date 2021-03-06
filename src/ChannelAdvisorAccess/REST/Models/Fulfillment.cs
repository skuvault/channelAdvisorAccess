﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChannelAdvisorAccess.REST.Models
{
	/// <summary>
	/// At least one fulfillment is created with every Order and includes the requested shipping carrier/class. This can be updated when marking the order shipped or modifying the fulfillment directly. 
	/// New fulfillments can be created as needed.
	/// </summary>
	public class Fulfillment
	{
		/// <summary>
		/// Uniquely identifies the fulfillment item within the ChannelAdvisor system
		/// </summary>
		public int ID { get; set; }
		/// <summary>
		/// Uniquely identifies the ChannelAdvisor account
		/// </summary>
		public int ProfileID { get; set; }
		/// <summary>
		/// Uniquely identifies the order within the ChannelAdvisor account.
		/// </summary>
		public int OrderID { get; set; }
		/// <summary>
		/// Timestamp when the fulfillment was created
		/// </summary>
		public DateTime? CreatedDateUtc { get; set; }
		/// <summary>
		/// Timestamp when the fulfillment was last updated.
		/// </summary>
		public DateTime? UpdatedDateUtc { get; set; }
		/// <summary>
		/// Indicates how the items will be fulfilled
		/// </summary>
		public FulfillmentType Type { get; set; }
		/// <summary>
		/// Indicates the progress of the fulfillment
		/// </summary>
		public FulfillmentDeliveryStatus DeliveryStatus { get; set; }
		/// <summary>
		/// Provided by a shipping carrier to track the progress of a shipment.
		/// </summary>
		public string TrackingNumber { get; set; }
		/// <summary>
		/// Identifies the shipping carrier or agent of delivery
		/// </summary>
		public string ShippingCarrier { get; set; }
		/// <summary>
		/// Identifies the class of shipment or delivery
		/// </summary>
		public string ShippingClass { get; set; }
		/// <summary>
		/// Identifies the distribution center from which the fulfillment will originate
		/// </summary>
		public int? DistributionCenterID { get; set; }
		/// <summary>
		/// Identifies the distribution center code generated by the marketplace. 
		/// Does not apply to all marketplace, and does not apply to Multi-Channel FBA (Fulfillment by Amazon)
		/// </summary>
		public string ExternalFulfillmentCenterCode { get; set; }
		/// <summary>
		/// The amount paid by the seller for shipping services, minus tax and insurance
		/// </summary>
		public decimal? ShippingCost { get; set; }
		/// <summary>
		/// The amount paid by the seller for shipment insurance
		/// </summary>
		public decimal? InsuranceCost { get; set; }
		/// <summary>
		/// The amount paid by the seller for taxes on shipping services
		/// </summary>
		public decimal? TaxCost { get; set; }
		/// <summary>
		/// 	Timestamp when the fulfillment was sent
		/// </summary>
		public DateTime? ShippedDateUtc { get; set; }
		/// <summary>
		/// Identifier provided by the seller. Amazon only allows integers
		/// </summary>
		public string SellerFulfillmentID { get; set; }
		/// <summary>
		/// Indicates if a shipping label has been generated through ChannelAdvisor
		/// </summary>
		public bool HasShippingLabel { get; set; }
		/// <summary>
		/// The fulfilled items which comprise the fulfillment.
		/// </summary>
		public FulfillmentItem[] Items { get; set; }
		/// <summary>
		/// Reference to the order to which the fulfillment belongs.
		/// </summary>
		public Order Order { get; set; }
	}

	/// <summary>
	/// The items to be fulfilled that belong to a Fulfillment. 
	/// Multiple FulfillmentItems may exist if multiple items are part of an order.
	/// </summary>
	public class FulfillmentItem
	{
		/// <summary>
		/// Uniquely identifies the fulfillment item within the ChannelAdvisor system
		/// </summary>
		public int ID { get; set; }
		/// <summary>
		/// Uniquely identifies the ChannelAdvisor account.
		/// </summary>
		public int ProfileID { get; set; }
		/// <summary>
		/// Uniquely identifies the fulfillment within the ChannelAdvisor account
		/// </summary>
		public int FulfillmentID { get; set; }
		/// <summary>
		/// Uniquely identifies the order within the ChannelAdvisor account
		/// </summary>
		public int OrderID { get; set; }
		/// <summary>
		/// Uniquely identifies the order item within the ChannelAdvisor account
		/// </summary>
		public int OrderItemID { get; set; }
		/// <summary>
		/// The number of units.
		/// </summary>
		public int Quantity { get; set; }
		/// <summary>
		/// Uniquely identifies the product within the ChannelAdvisor account
		/// </summary>
		public int ProductID { get; set; }
		/// <summary>
		/// Reference to the fulfillment to which the fulfillment item belongs
		/// </summary>
		public Fulfillment Fulfillment { get; set; }
		/// <summary>
		/// Reference to the order to which the fulfillment item belongs
		/// </summary>
		public Order Order { get; set; }
		/// <summary>
		/// If applicable, reference to the bundle component corresponding to the fulfillment item
		/// </summary>
		public OrderItemBundleComponent BundleComponent { get; set; }
	}

	public enum FulfillmentType
	{
		/// <summary>
		/// Items are shipped to the recipient
		/// </summary>
		Ship = 1,
		/// <summary>
		/// Items are picked up from a physical location by the recipient
		/// </summary>
		Pickup = 2,
		/// <summary>
		/// Items are shipped to a physical location then picked up by the recipient
		/// </summary>
		ShipToStore = 3,
		/// <summary>
		/// Items are delivered to the recipient by an intermediary
		/// </summary>
		Courier = 4
	}

	public enum FulfillmentDeliveryStatus
	{
		/// <summary>
		/// The fulfillment is unshipped. Making a request with this value will set a fulfillment or order to unshipped
		/// </summary>
		NoChange = 1,
		/// <summary>
		/// The fulfillment is in transit to a location where the recipient may pick it up. 
		/// This status is used for eBay In-Store-Pickup orders only.
		/// </summary>
		InTransit = 2,
		/// <summary>
		/// The fulfillment has arrived at the pickup location and is ready for the recipient to pickup. 
		/// This status is intended to be used for eBay In-Store-Pickup orders only.
		/// </summary>
		ReadyForPickup = 4,
		/// <summary>
		/// The fulfillment is complete and/or fully shipped.
		/// </summary>
		Complete = 8,
		/// <summary>
		/// The items on this fulfillment have been canceled and will not be fulfilled.
		/// </summary>
		Canceled = 13,
		/// <summary>
		/// The fulfillment is handled outside ChannelAdvisor. 
		/// Integrations where orders are imported into ChannelAdvisor but the retailer does not want ChannelAdvisor to manage any aspects of the order's fulfillment will have this status.
		/// </summary>
		ThirdPartyManaged = 26
	}

	public enum AdjustmentReason
	{
		GeneralAdjustment = 100,
		ItemNotAvailable = 101,
		CustomerReturnedItem = 102,
		CouldNotShip = 103,
		AlternateItemProvided = 104,
		BuyerCancelled = 105,
		CustomerExchange = 106,
		MerchandiseNotReceived = 107,
		ShippingAddressUndeliverable = 108
	}

	public enum AdjustmentType
	{
		/// <summary>
		///	Generally applied after fulfillment
		/// </summary>
		Refund = 0,
		/// <summary>
		///	Generally applied before fulfillment
		/// </summary>
		Cancellation = 1,
		/// <summary>
		///	Applied when an item is in dispute (used for eBay buyer-requested cancel/refunds)
		/// </summary>
		Dispute = 2
	}

	public enum AdjustmentRequestStatus
	{
		/// <summary>
		///	The operation failed
		/// </summary>
		Error = -1,
		/// <summary>
		/// Queued for processing
		/// </summary>
		SubmittedNotProcessed = 0,
		/// <summary>
		/// New buyer-initiated return awaiting the seller's approval or rejection
		/// </summary>
		NewRma = 1,
		/// <summary>
		/// Queued for approval communication with the marketplace
		/// </summary>
		PendingApproval = 2,
		/// <summary>
		/// Approval communication is in progress
		/// </summary>
		ProcessingApproval = 3,
		/// <summary>
		/// Approval complete; awaiting the seller's approval or rejection of the return shipment
		/// </summary>
		ReadyForReturn = 4,
		/// <summary>
		/// Queued for return approval communication with the marketplace
		/// </summary>
		PendingReturn = 5,
		/// <summary>
		/// Return approval communication is in progress
		/// </summary>
		ProcessingReturnApproval = 6,
		/// <summary>
		/// Queued for rejection communication with the marketplace
		/// </summary>
		PendingRejection = 7,
		/// <summary>
		/// Rejection communication is in progress
		/// </summary>
		ProcessingRejection = 8,
		/// <summary>
		/// Initial processing is complete; waiting for response
		/// </summary>
		ProcessedNotAckowledged = 10,
		/// <summary>
		/// Queued for return rejection communication with the marketplace
		/// </summary>
		PendingReturnRejection = 11,
		/// <summary>
		/// Return rejection communication is in progress
		/// </summary>
		ProcessingReturnRejection = 12,
		/// <summary>
		/// Response received; completing final processing
		/// </summary>
		AcknowledgedPostProcessingNotComplete = 20,
		/// <summary>
		/// Successful and complete
		/// </summary>
		PostProcessingComplete = 30,
		/// <summary>
		/// Successfully completed rejection processing.  No refund occurred.
		/// </summary>
		RejectionCompleted = 31,
		/// <summary>
		/// The buyer-initiated return was imported for information purposes but was not processed
		/// </summary>
		InformationOnly = 32,
		/// <summary>
		/// Incomplete
		/// </summary>
		NoChange = -999
	}
}
