﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TripShare.Models;
using Microsoft.AspNetCore.Authorization;
using TripShare.Abstract;
using TripShare.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.NodeServices;
using TripShare.Models.HomeViewModels;

namespace TripShare.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ITripRepository tripRepository;
        private readonly INodeServices nodeServices;
        private readonly IRefundRepository refundRepository;
        private string GetScriptLocation() => "./Node/Blockchain.js";
        private readonly IBlockchainRepository blockchain;

        public HomeController(UserManager<ApplicationUser> _userManager, ITripRepository _tripRepository, INodeServices _nodeServices, IRefundRepository _refundRepository, IBlockchainRepository _blockchain)
        {
            userManager = _userManager;
            tripRepository = _tripRepository;
            nodeServices = _nodeServices;
            refundRepository = _refundRepository;
            blockchain = _blockchain;
        }

        private string GetNetwork()
        {
            return "TestNet";
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult TripInfoByString(TripInfoByStringViewModel model)
        {
            List<Trip> trips = new List<Trip>();
            trips = tripRepository.FindTrip(model.From, model.To, model.Date).ToList();
            foreach (var t in trips)
            {
                t.Passengers = tripRepository.FindPassenger(t).ToList();
            }
            return TripInfoByString(trips);
        }

        public IActionResult TripInfoByString([FromRoute] List<Trip> trip)
        {
            return View(trip);
        }

        [HttpPost, Authorize, HttpGet]
        public async Task<IActionResult> TripBuySeat(string id)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
            }

            if (user.ScriptHash == null)
                return RedirectToAction("RegisterWalletAsk");

            bool bought = tripRepository.BuySeat(id, user.Id);
            var res = await blockchain.InvokeContractReserveSeat(NETWORK_TYPE.TESTNET, user.Wif, id, user.ScriptHash, 2);

            if (res)
            {
                if (bought)
                {
                    return RedirectToAction("TripBuySucc");
                }
                return RedirectToAction("TripBuySucc");
            }

            if (bought)
            {
                return RedirectToAction("TripBuySucc");
            }
            else
            {
                return View();
            }
        }

        public IActionResult TripBuySucc()
        {
            return View();
        }

        [HttpPost, HttpGet]
        public IActionResult TripInfoById(string id)
        {
            Trip trip = tripRepository.FindTrip(id);
            if (trip != null)
                trip.Passengers = tripRepository.FindPassenger(trip).ToList();
            return TripInfoById(trip);
        }

        public IActionResult TripInfoById([FromRoute]Trip trip)
        {

            return View(trip);
        }

        [HttpGet]
        public IActionResult Refund()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Refund(RefundViewModel model)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
            }

            ViewData["Message"] = "";
            var added = refundRepository.AddRefund(new Refund { Address = model.Address, Amount = model.Amount, Done = false });
            var bc = await blockchain.InvokeContractRequestRefund(NETWORK_TYPE.TESTNET, user.Wif, user.ScriptHash, 10, 2);

            if (bc)
            {
                if (added)
                {
                    ViewData["Message"] = "Transaction and db added";
                    return View(model);
                }
                ViewData["Message"] = "transaction done, db fail";
                return View(model);
            }
            ViewData["Message"] = added ? "Refund was requested (only db)" : "Failed to request";
            return View(model);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult RegisterWalletAsk()
        {
            return View();
        }
    }
}
